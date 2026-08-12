using Microsoft.Agents.AI;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public class AgentFactChecker
{
    private readonly AIAgent _agent;
    private readonly RateLimiter? _rateLimiter;
    private readonly IRetryService _retryService;
    private readonly IOperationalEventSink _eventSink;

    // Delegate for reporting alerts so we don't depend on UI directly
    public event Func<string, Task>? OnFactCheckAlert;

    public AgentFactChecker(AIAgent agent, RateLimiter? rateLimiter, IRetryService retryService, IOperationalEventSink? eventSink = null)
    {
        _agent = agent;
        _rateLimiter = rateLimiter;
        _retryService = retryService;
        _eventSink = eventSink ?? NullOperationalEventSink.Instance;
    }

    public AgentFactChecker(AIAgent agent, RateLimiter? rateLimiter)
        : this(agent, rateLimiter, new RetryService(null))
    {
    }

    public static string BuildSystemPrompt()
    {
        return @"You are a rigorous Fact-Checking Agent.
Your role:
- Monitor the conversation for factual claims, statistics, and assertions.
- Verify them against your training data.
- If a claim is dubious, hallucinated, or definitely false, you must Flag it.
- If a claim is generally true or subjective, do nothing.

Output Format:
- If no issues: Return 'PASS'
- If issues found: Return 'FLAG: <Description of the error and correction>'";
    }

    public async Task CheckAsync(string recentMessage, CancellationToken cancellationToken = default)
    {
        // Don't check empty messages or short acknowledgments
        if (recentMessage.Length < 20) return;

        var prompt = $"Verify the following text for factual accuracy:\n\n{recentMessage}";

        try
        {
            if (_rateLimiter != null)
            {
                await _rateLimiter.ThrottleAsync(_rateLimiter.EstimateTokens(prompt), cancellationToken);
            }

            var response = await _retryService.ExecuteAsync(
                async cancellationToken => await _agent.RunAsync(prompt, cancellationToken: cancellationToken),
                "Fact Check",
                cancellationToken);

            var result = response.ToString().Trim();

            if (!result.StartsWith("PASS", StringComparison.OrdinalIgnoreCase))
            {
                if (OnFactCheckAlert != null)
                {
                    await OnFactCheckAlert.Invoke(result);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _eventSink.Publish(new OperationalEvent(
                OperationalEventKind.OperationFailure,
                "Fact check")
            {
                Exception = ex
            });
        }
    }
}
