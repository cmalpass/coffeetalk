using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class AgentPersonaTests
{
    [Fact]
    public async Task RespondAsync_ReturnsGenericFailureTextWithoutExceptionDetails()
    {
        const string secret = "internal-host-and-connection-details";
        var persona = new AgentPersona(
            new TestAIAgent(new InvalidOperationException(secret)),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1);

        var result = await persona.RespondAsync("topic", new List<string>());

        Assert.Equal("Error: An unexpected error occurred.", result);
        Assert.DoesNotContain(secret, result);
    }

    [Fact]
    public async Task RespondStreamingAsync_EmitsChunksAndPreservesOrder()
    {
        var agent = new TestAIAgent(
            "buffered",
            ["first ", "second"],
            new InvalidOperationException("stream failed after output"));
        var persona = new AgentPersona(
            agent,
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            providerConfig: new LlmProviderConfig { Type = "openai" });

        var chunks = new List<string>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var chunk in persona.RespondStreamingAsync("topic", []))
                chunks.Add(chunk);
        });

        Assert.Equal(["first ", "second"], chunks);
        Assert.Equal(0, agent.Calls);
        Assert.Equal(1, agent.StreamingCalls);
    }

    [Fact]
    public async Task RespondStreamingAsync_UsesBufferedFallbackForUnsupportedOllama()
    {
        var agent = new TestAIAgent("buffered");
        var persona = new AgentPersona(
            agent,
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            providerConfig: new LlmProviderConfig { Type = "ollama" });

        var chunks = new List<string>();
        await foreach (var chunk in persona.RespondStreamingAsync("topic", []))
            chunks.Add(chunk);

        Assert.Equal(["buffered"], chunks);
        Assert.Equal(1, agent.Calls);
        Assert.Equal(0, agent.StreamingCalls);
    }

    [Fact]
    public async Task RespondStreamingAsync_PropagatesCancellationDuringEnumeration()
    {
        var agent = new TestAIAgent("buffered", ["first ", "second"]);
        var persona = new AgentPersona(
            agent,
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            providerConfig: new LlmProviderConfig { Type = "openai" });
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in persona.RespondStreamingAsync("topic", [], cancellation.Token))
                cancellation.Cancel();
        });
    }
}
