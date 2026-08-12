using System.Collections.Concurrent;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public class RateLimiter
{
    private readonly RateLimitConfig? _cfg;
    private readonly object _convLock = new();
    private DateTime _windowStart = DateTime.UtcNow;
    private int _requestsInWindow = 0;
    private int _tokensInWindow = 0;

    private int _convRequests = 0;
    private int _convTokens = 0;

    public RateLimiter(RateLimitConfig? cfg)
    {
        _cfg = cfg;
    }

    public void ResetConversation()
    {
        lock (_convLock)
        {
            _convRequests = 0;
            _convTokens = 0;
        }
    }

    private void RollWindow()
    {
        if (_cfg?.RequestsPerMinute == null && _cfg?.TokensPerMinute == null) return;
        var now = DateTime.UtcNow;
        if ((now - _windowStart).TotalSeconds >= 60)
        {
            _windowStart = now;
            _requestsInWindow = 0;
            _tokensInWindow = 0;
        }
    }

    public async Task ThrottleAsync(int estimatedTokens, CancellationToken ct = default)
    {
        if (_cfg == null) return;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var delayMs = 0;

            lock (_convLock)
            {
                RollWindow();

                // Per-conversation caps
                if (_cfg.MaxRequestsPerConversation.HasValue && _convRequests + 1 > _cfg.MaxRequestsPerConversation.Value)
                {
                    throw new InvalidOperationException($"Conversation request cap reached ({_cfg.MaxRequestsPerConversation})");
                }
                if (_cfg.MaxTokensPerConversation.HasValue && _convTokens + estimatedTokens > _cfg.MaxTokensPerConversation.Value)
                {
                    throw new InvalidOperationException($"Conversation token cap reached ({_cfg.MaxTokensPerConversation})");
                }

                // Per-minute caps
                if (_cfg.RequestsPerMinute.HasValue && _requestsInWindow + 1 > _cfg.RequestsPerMinute.Value)
                {
                    var secondsLeft = 60 - (int)(DateTime.UtcNow - _windowStart).TotalSeconds;
                    delayMs = Math.Max(100, secondsLeft * 1000);
                }
                if (_cfg.TokensPerMinute.HasValue && _tokensInWindow + estimatedTokens > _cfg.TokensPerMinute.Value)
                {
                    var secondsLeft = 60 - (int)(DateTime.UtcNow - _windowStart).TotalSeconds;
                    delayMs = Math.Max(delayMs, Math.Max(100, secondsLeft * 1000));
                }

                if (delayMs > 0)
                {
                    // Will delay outside lock
                }
                else
                {
                    // Reserve atomically
                    _requestsInWindow += 1;
                    _tokensInWindow += estimatedTokens;
                    _convRequests += 1;
                    _convTokens += estimatedTokens;
                    return;
                }
            }

            await Task.Delay(delayMs, ct);
        }
    }

    public int EstimateTokens(string text, double charsPerToken)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var est = (int)Math.Ceiling(text.Length / Math.Max(1.0, charsPerToken));
        return Math.Max(1, est);
    }

    public int EstimateTokens(string text)
    {
        var cpt = _cfg?.ApproxCharsPerToken ?? 4.0;
        return EstimateTokens(text, cpt);
    }

    public void AccountAdditionalTokens(int tokens)
    {
        if (_cfg == null) return;
        lock (_convLock)
        {
            RollWindow();
            _tokensInWindow += Math.Max(0, tokens);
            _convTokens += Math.Max(0, tokens);
            if (_cfg.TokensPerMinute.HasValue && _tokensInWindow > _cfg.TokensPerMinute.Value)
            {
                throw new InvalidOperationException($"Per-minute token cap reached ({_cfg.TokensPerMinute})");
            }
            if (_cfg.MaxTokensPerConversation.HasValue && _convTokens > _cfg.MaxTokensPerConversation.Value)
            {
                throw new InvalidOperationException($"Conversation token cap reached ({_cfg.MaxTokensPerConversation})");
            }
        }
    }
}
