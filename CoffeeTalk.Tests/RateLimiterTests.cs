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
}
