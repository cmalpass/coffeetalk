using Microsoft.Agents.AI;
using CoffeeTalk.Models;
using Spectre.Console;

namespace CoffeeTalk.Services;

public class AgentFactChecker
{
    private readonly AIAgent _agent;
    private readonly RateLimiter? _rateLimiter;

    public AgentFactChecker(AIAgent agent, RateLimiter? rateLimiter)
    {
        _agent = agent;
        _rateLimiter = rateLimiter;
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

    public async Task CheckAsync(string recentMessage)
    {
        // Don't check empty messages or short acknowledgments
        if (recentMessage.Length < 20) return;

        var prompt = $"Verify the following text for factual accuracy:\n\n{recentMessage}";

        try
        {
            if (_rateLimiter != null)
            {
                await _rateLimiter.ThrottleAsync(_rateLimiter.EstimateTokens(prompt));
            }

            var response = await RetryHandler.ExecuteWithRetryAsync(
                async () => await _agent.RunAsync(prompt),
                "Fact Check");

            var result = response.ToString().Trim();

            if (!result.StartsWith("PASS", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"\n[bold red]🕵️ Fact Checker Alert:[/]");
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(result)}[/]");
            }
        }
        catch (Exception)
        {
            // Fail silently to not disrupt flow
        }
    }
}
