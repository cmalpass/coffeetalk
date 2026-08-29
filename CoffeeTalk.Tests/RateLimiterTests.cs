using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class RateLimiterTests
{
    [Fact]
    public async Task ThrottleAsync_ReservesRequestsAtomicallyUnderConcurrency()
    {
        var limiter = new RateLimiter(new RateLimitConfig { RequestsPerMinute = 1 });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => limiter.ThrottleAsync(1, cancellation.Token))
            .ToArray();

        var results = await Task.WhenAll(tasks.Select(async task =>
        {
            try
            {
                await task;
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(result => result));
    }

    [Fact]
    public async Task ThrottleAsync_EnforcesConversationCaps()
    {
        var limiter = new RateLimiter(new RateLimitConfig
        {
            MaxRequestsPerConversation = 1,
            MaxTokensPerConversation = 3
        });

        await limiter.ThrottleAsync(3);

        await Assert.ThrowsAsync<InvalidOperationException>(() => limiter.ThrottleAsync(1));
    }

    [Fact]
    public void EstimateTokens_UsesConfiguredApproximationAndHandlesEmptyText()
    {
        var limiter = new RateLimiter(new RateLimitConfig { ApproxCharsPerToken = 2 });

        Assert.Equal(2, limiter.EstimateTokens("abc"));
        Assert.Equal(0, limiter.EstimateTokens(string.Empty));
    }

    [Fact]
    public async Task AccountAdditionalTokens_EnforcesPerMinuteTokenCap()
    {
        var limiter = new RateLimiter(new RateLimitConfig { TokensPerMinute = 3 });

        await limiter.ThrottleAsync(2);

        Assert.Throws<InvalidOperationException>(() => limiter.AccountAdditionalTokens(2));
    }

    [Fact]
    public async Task ThrottleAsync_AllowsExactlyRequestsPerMinuteBeforeWaiting()
    {
        var limiter = new RateLimiter(new RateLimitConfig { RequestsPerMinute = 3 });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Exactly the configured request quota passes without waiting...
        for (var i = 0; i < 3; i++)
        {
            await limiter.ThrottleAsync(1, cancellation.Token);
        }

        // ...and the next request is blocked, so it waits and is cancelled within the short window.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.ThrottleAsync(1, cancellation.Token));
    }

    [Fact]
    public async Task ThrottleAsync_AllowsFullPerMinuteTokenQuotaBeforeWaiting()
    {
        var limiter = new RateLimiter(new RateLimitConfig { TokensPerMinute = 3 });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        for (var i = 0; i < 3; i++)
        {
            await limiter.ThrottleAsync(1, cancellation.Token);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.ThrottleAsync(1, cancellation.Token));
    }

    [Fact]
    public void ComputeWaitDelay_IsCappedAtConfiguredMaxPerMinuteDelaySeconds()
    {
        var limiter = new RateLimiter(new RateLimitConfig
        {
            RequestsPerMinute = 1,
            MaxPerMinuteDelaySeconds = 2
        });

        // Window started ~40s ago: uncapped the wait would be ~20s; the cap bounds it at 2s.
        var delay = ComputeWaitDelay(limiter, DateTime.UtcNow.AddSeconds(-40));

        Assert.Equal(TimeSpan.FromSeconds(2), delay);
    }

    [Fact]
    public void ComputeWaitDelay_DefaultCapBoundsLongRemainingWindow()
    {
        var limiter = new RateLimiter(new RateLimitConfig { RequestsPerMinute = 1 });

        var delay = ComputeWaitDelay(limiter, DateTime.UtcNow.AddSeconds(-1));

        // ~59s remaining in the window, but the default cap keeps the single sleep at 30s max.
        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void ComputeWaitDelay_AppliesBoundedJitterOnlyWhenConfigured()
    {
        var jittered = new RateLimiter(new RateLimitConfig
        {
            RequestsPerMinute = 1,
            MaxPerMinuteDelaySeconds = 2,
            JitterMaxMilliseconds = 500
        });
        var plain = new RateLimiter(new RateLimitConfig
        {
            RequestsPerMinute = 1,
            MaxPerMinuteDelaySeconds = 2
        });

        var windowStart = DateTime.UtcNow.AddSeconds(-40);
        var jitteredDelays = new HashSet<int>();
        for (var i = 0; i < 200; i++)
        {
            var delay = ComputeWaitDelay(jittered, windowStart);
            // Base wait is 2s; jitter extends it within the 500ms bound, never beyond the cap of 2500ms.
            Assert.InRange(delay.TotalMilliseconds, 2_000, 2_500);
            jitteredDelays.Add((int)delay.TotalMilliseconds);
            // Without jitter the wait is exactly the capped base.
            Assert.Equal(TimeSpan.FromSeconds(2), ComputeWaitDelay(plain, windowStart));
        }

        Assert.True(jitteredDelays.Count > 1, "Configured jitter should vary the wait time.");
    }

    private static TimeSpan ComputeWaitDelay(RateLimiter limiter, DateTime windowStart)
    {
        var method = typeof(RateLimiter).GetMethod("ComputeWaitDelay",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ComputeWaitDelay not found");

        var delayMs = (int)(method.Invoke(limiter, new object[] { windowStart })
            ?? throw new InvalidOperationException("ComputeWaitDelay returned null"));

        return TimeSpan.FromMilliseconds(delayMs);
    }
}
