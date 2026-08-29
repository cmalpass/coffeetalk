using System.Collections.Concurrent;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public class RateLimiter
{
    private readonly RateLimitConfig? _cfg;
    private readonly object _convLock = new();
    private DateTime _windowStart = DateTime.UtcNow;
    private int _requestsInWindow;
    private int _tokensInWindow;

    private int _convRequests;
    private int _convTokens;

    private readonly Random _jitter = new();

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

            var shouldWait = false;
            DateTime windowStart = default;

            lock (_convLock)
            {
                RollWindow();

                // Per-conversation caps
                if (_cfg.MaxRequestsPerConversation.HasValue && _convRequests >= _cfg.MaxRequestsPerConversation.Value)
                {
                    throw new InvalidOperationException($"Conversation request cap reached ({_cfg.MaxRequestsPerConversation})");
                }
                if (_cfg.MaxTokensPerConversation.HasValue && _convTokens + estimatedTokens > _cfg.MaxTokensPerConversation.Value)
                {
                    throw new InvalidOperationException($"Conversation token cap reached ({_cfg.MaxTokensPerConversation})");
                }

                // Per-minute caps. The boundary is inclusive: exactly RequestsPerMinute
                // reservations are allowed and the next call waits, so the configured
                // quota is fully usable. Reservation is applied only when the request
                // passes; a blocked caller wakes, rolls the window, and re-checks.
                if (ShouldWait(_requestsInWindow, _tokensInWindow, estimatedTokens))
                {
                    shouldWait = true;
                    windowStart = _windowStart;
                }
                else
                {
                    _requestsInWindow += 1;
                    _tokensInWindow += estimatedTokens;
                    _convRequests += 1;
                    _convTokens += estimatedTokens;
                    return;
                }
            }

            if (shouldWait)
            {
                // Blocked: compute the capped, lightly-jittered wait outside the lock. On
                // wake we re-enter, roll the window if needed, and re-check the boundary so
                // the decision stays correct.
                var delay = ComputeWaitDelay(windowStart);
                await Task.Delay(delay, ct);
            }
        }
    }

    private bool ShouldWait(int requestsInWindow, int tokensInWindow, int estimatedTokens)
    {
        if (_cfg == null) return false;

        // A single request larger than the per-minute token quota can never fit; fail fast.
        if (_cfg.TokensPerMinute.HasValue && estimatedTokens > _cfg.TokensPerMinute.Value)
        {
            throw new InvalidOperationException($"Request token estimate exceeds per-minute cap ({_cfg.TokensPerMinute})");
        }
        if (_cfg.RequestsPerMinute.HasValue && requestsInWindow >= _cfg.RequestsPerMinute.Value) return true;
        if (_cfg.TokensPerMinute.HasValue && tokensInWindow + estimatedTokens > _cfg.TokensPerMinute.Value) return true;
        return false;
    }

    private int ComputeWaitDelay(DateTime windowStart)
    {
        var delaySeconds = 60 - (int)(DateTime.UtcNow - windowStart).TotalSeconds;
        var maxDelaySeconds = Math.Max(1, _cfg?.MaxPerMinuteDelaySeconds ?? 30);
        var secondsToWait = Math.Min(Math.Max(delaySeconds, 1), maxDelaySeconds);
        var delayMs = secondsToWait * 1000;

        // Mild jitter desynchronizes concurrent callers that hit the same window; it only
        // lengthens the capped base delay, so the cap remains an upper bound on the sleep.
        if (_cfg?.JitterMaxMilliseconds is > 0)
        {
            delayMs += _jitter.Next(0, _cfg.JitterMaxMilliseconds.Value);
        }

        return delayMs;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "The configured overload remains instance-based; this overload is retained as part of the public limiter API.")]
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
