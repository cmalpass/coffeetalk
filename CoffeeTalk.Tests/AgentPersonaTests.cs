using CoffeeTalk.Models;
using CoffeeTalk.Services;
using CoffeeTalk.Core.Interfaces;

namespace CoffeeTalk.Tests;

public sealed class AgentPersonaTests
{
    [Fact]
    public async Task RespondAsync_PropagatesProviderFailureInsteadOfFabricatingErrorResponse()
    {
        const string secret = "internal-host-and-connection-details";
        var persona = new AgentPersona(
            new TestAIAgent(new InvalidOperationException(secret)),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => persona.RespondAsync("topic", new List<string>()));

        // The original exception is rethrown so the orchestrator's failure handling can
        // surface it; no fabricated "Error: ..." assistant response is produced that could
        // be appended to conversation history.
        Assert.Equal(secret, ex.Message);
    }

    [Fact]
    public async Task RespondAsync_PropagatesTimeoutFailure()
    {
        var persona = new AgentPersona(
            new TestAIAgent(new TimeoutException("timed out")),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1);

        await Assert.ThrowsAsync<TimeoutException>(
            () => persona.RespondAsync("topic", new List<string>()));
    }

    [Fact]
    public async Task RespondAsync_RethrowsCancellation()
    {
        var persona = new AgentPersona(
            new TestAIAgent("ok"),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => persona.RespondAsync("topic", new List<string>(), cancellation.Token));
    }

    [Fact]
    public async Task AssessConsensusAsync_PropagatesProviderFailure()
    {
        var persona = new AgentPersona(
            new TestAIAgent(new HttpRequestException("network error")),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(new RetryConfig { InitialDelaySeconds = 0 }));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => persona.AssessConsensusAsync("CONCLUDE", new List<string>()));
    }

    [Fact]
    public async Task FallbackToBufferedAsync_PropagatesProviderFailure()
    {
        var persona = new AgentPersona(
            new TestAIAgent(new TimeoutException("fallback timed out")),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            providerConfig: new LlmProviderConfig { Type = "ollama" });

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var _ in persona.RespondStreamingAsync("topic", []))
            {
            }
        });
    }

    [Fact]
    public async Task RespondAsync_UsesBoundedStatelessContextAcrossTurns()
    {
        var agent = new TestAIAgent("ok");
        var document = new CollaborativeMarkdownDocument();
        document.AppendParagraph(new string('x', AgentContextPolicy.MaxDocumentCharacters * 2));
        var persona = new AgentPersona(
            agent,
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            document,
            null,
            maxTurns: 3,
            agentCount: 1);

        var history = new List<string> { "first turn", "second turn" };
        await persona.RespondAsync("current", history);
        await persona.RespondAsync("next", history);

        Assert.Equal(2, agent.Calls);
        Assert.Equal(2, agent.Prompts.Count);
        Assert.All(agent.Prompts, prompt => Assert.InRange(prompt.Length, 1, AgentContextPolicy.MaxPromptCharacters));
        Assert.Contains("first turn", agent.Prompts[0]);
        Assert.Contains("second turn", agent.Prompts[1]);
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
    public async Task RespondStreamingAsync_RecordsRecoveredBufferedFallback()
    {
        var events = new RecordingEventSink();
        var agent = new TestAIAgent(
            "buffered",
            [],
            new InvalidOperationException("stream unavailable"),
            failBeforeStreamingOutput: true);
        var persona = new AgentPersona(
            agent,
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            providerConfig: new LlmProviderConfig { Type = "openai" },
            eventSink: events);

        var chunks = new List<string>();
        await foreach (var chunk in persona.RespondStreamingAsync("topic", []))
            chunks.Add(chunk);

        Assert.Equal(["buffered"], chunks);
        Assert.Contains(events.Events, item => item.Kind == OperationalEventKind.RequestFallback);
        Assert.Contains(events.Events, item => item.Kind == OperationalEventKind.RequestCompleted);
        Assert.DoesNotContain(events.Events, item => item.Kind == OperationalEventKind.RequestFailed);
    }

    [Fact]
    public async Task RespondStreamingAsync_ThinkingTelemetryDoesNotContainRawThinkingByDefault()
    {
        const string reasoningSecret = "user-supplied-topic-and-document-fragments";
        var events = new RecordingEventSink();
        var agent = new TestAIAgent(
            "buffered",
            ["chunk"],
            reasoningChunks: [reasoningSecret]);
        var persona = new AgentPersona(
            agent,
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            providerConfig: new LlmProviderConfig { Type = "openai" },
            eventSink: events);

        var chunks = new List<string>();
        await foreach (var chunk in persona.RespondStreamingAsync("topic", []))
            chunks.Add(chunk);

        Assert.Equal(["chunk"], chunks);
        var thinking = Assert.Single(events.Events, item => item.Kind == OperationalEventKind.RequestThinking);
        Assert.DoesNotContain(reasoningSecret, thinking.Reason);
        Assert.NotNull(thinking.ThinkingCharacters);
        Assert.True(thinking.ThinkingCharacters > 0);
        Assert.NotNull(thinking.EstimatedThinkingTokens);
        Assert.NotNull(thinking.ThinkingDurationMilliseconds);
    }

    [Fact]
    public async Task RespondStreamingAsync_ThinkingTelemetryIncludesContentWhenOptedIn()
    {
        const string reasoning = "thinking content";
        var events = new RecordingEventSink();
        var agent = new TestAIAgent(
            "buffered",
            ["chunk"],
            reasoningChunks: [reasoning]);
        var persona = new AgentPersona(
            agent,
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            providerConfig: new LlmProviderConfig { Type = "openai" },
            eventSink: events,
            includeThinkingInTelemetry: true);

        var chunks = new List<string>();
        await foreach (var chunk in persona.RespondStreamingAsync("topic", []))
            chunks.Add(chunk);

        Assert.Equal(["chunk"], chunks);
        var thinking = Assert.Single(events.Events, item => item.Kind == OperationalEventKind.RequestThinking);
        Assert.Contains(reasoning, thinking.Reason);
    }

    [Fact]
    public async Task RespondStreamingAsync_RecoveredFallbackDoesNotExposeExceptionMessageInReason()
    {
        const string exceptionSecret = "connection-string-with-password=secret";
        var events = new RecordingEventSink();
        var agent = new TestAIAgent(
            "buffered",
            [],
            new InvalidOperationException(exceptionSecret),
            failBeforeStreamingOutput: true);
        var persona = new AgentPersona(
            agent,
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            providerConfig: new LlmProviderConfig { Type = "openai" },
            eventSink: events);

        var chunks = new List<string>();
        await foreach (var chunk in persona.RespondStreamingAsync("topic", []))
            chunks.Add(chunk);

        Assert.Equal(["buffered"], chunks);
        var fallback = Assert.Single(events.Events, item => item.Kind == OperationalEventKind.RequestFallback);
        Assert.DoesNotContain(exceptionSecret, fallback.Reason);
        Assert.Contains("InvalidOperationException", fallback.Reason);
    }

    private sealed class RecordingEventSink : IOperationalEventSink
    {
        public List<OperationalEvent> Events { get; } = [];

        public void Publish(OperationalEvent operationalEvent) => Events.Add(operationalEvent);
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
