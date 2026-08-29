using Xunit;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Services;
using CoffeeTalk.Models;
using CoffeeTalk.Gui.Services;
using Microsoft.Agents.AI;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Moq;

namespace CoffeeTalk.Tests;

public class OrchestratorTests
{
    [Fact]
    public async Task StartConversationAsync_ShouldShowError_WhenNoPersonas()
    {
        // Arrange
        var mockUi = new Mock<IUserInterface>();
        var settings = new AppSettings();
        var doc = new CollaborativeMarkdownDocument();

        var orchestrator = new AgentConversationOrchestrator(
            mockUi.Object,
            new List<AgentPersona>(),
            doc,
            settings
        );

        // Act
        await orchestrator.StartConversationAsync("Test Topic");

        // Assert
        mockUi.Verify(ui => ui.ShowErrorAsync(It.Is<string>(s => s.Contains("No personas configured"))), Times.Once);
    }

    [Fact]
    public async Task StartConversationAsync_ShowsGenericErrorForUnexpectedOrchestratorFailure()
    {
        var ui = new BlazorUserInterface();
        var persona = new AgentPersona(
            new TestAIAgent(new InvalidOperationException("internal stack details")),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 1,
            agentCount: 1);
        var settings = new AppSettings { MaxConversationTurns = 1 };
        var orchestrator = new AgentConversationOrchestrator(
            ui,
            new List<AgentPersona> { persona },
            new CollaborativeMarkdownDocument(),
            settings);

        await orchestrator.StartConversationAsync("topic");

        Assert.Contains(ui.Messages, message =>
            message.Content.Contains("An unexpected error occurred."));
        Assert.DoesNotContain(ui.Messages, message => message.Content.Contains("internal stack details"));
    }

    [Fact]
    public async Task OrchestratedConversation_StopsAfterConsensusBudgetWhenPersonasDissent()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-consensus-tests", Guid.NewGuid().ToString("N"));
        var doc = new CollaborativeMarkdownDocument(new ApplicationDataPathResolver(root));
        var persona = new AgentPersona(
            new TestAIAgent("CONSENSUS: NO\nReason: The recommendation is incomplete."),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            doc,
            null,
            maxTurns: 1,
            agentCount: 1);
        var orchestrator = new AgentOrchestrator(
            new TestAIAgent("CONCLUDE\nReason: ready to finish"),
            new OrchestratorConfig { Enabled = true },
            doc,
            [persona]);
        var ui = new BlazorUserInterface();
        var conversation = new AgentConversationOrchestrator(
            ui,
            [persona],
            doc,
            new AppSettings
            {
                MaxConversationTurns = 1,
                Orchestrator = new OrchestratorConfig { MaxConsensusAttempts = 1 }
            },
            orchestrator);

        try
        {
            await conversation.StartConversationAsync("Test topic");

            Assert.Contains(ui.Messages, message =>
                message.Content.Contains("Consensus was not reached after 1 attempt", StringComparison.Ordinal));
            Assert.Equal(ConversationTerminationReason.ConsensusBudgetExhausted, ui.TerminationReason);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OrchestratedConversation_RecordsConsensusReachedTermination()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-consensus-tests", Guid.NewGuid().ToString("N"));
        var doc = new CollaborativeMarkdownDocument(new ApplicationDataPathResolver(root));
        var persona = new AgentPersona(
            new TestAIAgent("CONSENSUS: YES\nReason: The recommendation is complete."),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            doc,
            null,
            maxTurns: 1,
            agentCount: 1);
        var orchestrator = new AgentOrchestrator(
            new TestAIAgent("CONCLUDE\nReason: ready to finish"),
            new OrchestratorConfig { Enabled = true },
            doc,
            [persona]);
        var ui = new BlazorUserInterface();
        var conversation = new AgentConversationOrchestrator(
            ui,
            [persona],
            doc,
            new AppSettings { MaxConversationTurns = 1 },
            orchestrator);

        try
        {
            await conversation.StartConversationAsync("Test topic");

            Assert.Equal(ConversationTerminationReason.ConsensusReached, ui.TerminationReason);
            Assert.Contains(ui.Messages, message =>
                message.Content.Contains("Consensus reached. Conversation ended successfully", StringComparison.Ordinal));
            Assert.DoesNotContain(ui.Messages, message =>
                message.Content.Contains("Maximum turns", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OrchestratedConversation_ConsensusAttemptsCappedIndependentlyOfTurnBudget()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-consensus-tests", Guid.NewGuid().ToString("N"));
        var doc = new CollaborativeMarkdownDocument(new ApplicationDataPathResolver(root));
        var persona = new AgentPersona(
            new TestAIAgent("CONSENSUS: NO\nReason: Always dissenting."),
            new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
            doc,
            null,
            maxTurns: 1,
            agentCount: 1,
            new RetryService(null));
        var orchestrator = new AgentOrchestrator(
            new TestAIAgent("Conclude\nReason: ready to finish"),
            new OrchestratorConfig { Enabled = true, MaxConsensusAttempts = 2 },
            doc,
            [persona]);
        var ui = new BlazorUserInterface();
        var conversation = new AgentConversationOrchestrator(
            ui,
            [persona],
            doc,
            new AppSettings
            {
                MaxConversationTurns = 50,
                Orchestrator = new OrchestratorConfig { MaxConsensusAttempts = 2 }
            },
            orchestrator);

        try
        {
            await conversation.StartConversationAsync("Test topic");

            // The consensus budget (2) is far below the turn budget (50), so the
            // conversation must stop at 2 consensus attempts, not retry until turns exhaust.
            Assert.Contains(ui.Messages, message =>
                message.Content.Contains("Consensus was not reached after 2 attempt", StringComparison.Ordinal));
            Assert.Equal(ConversationTerminationReason.ConsensusBudgetExhausted, ui.TerminationReason);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OrchestratedConversation_BoundsConsensusCallConcurrency()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-consensus-tests", Guid.NewGuid().ToString("N"));
        var doc = new CollaborativeMarkdownDocument(new ApplicationDataPathResolver(root));
        var tracker = new ConsensusConcurrencyTracker();
        var agents = Enumerable.Range(0, 5)
            .Select(i => new ConcurrencyTrackingAgent("CONSENSUS: NO\nReason: Busy checking.", tracker))
            .ToList();
        var personas = agents.Select((agent, i) => new AgentPersona(
            agent,
            new PersonaConfig { Name = $"Persona{i}", SystemPrompt = $"You are Persona{i}." },
            doc,
            null,
            maxTurns: 1,
            agentCount: 5,
            new RetryService(null))).ToList();
        var orchestrator = new AgentOrchestrator(
            new TestAIAgent("Conclude\nReason: ready to finish"),
            new OrchestratorConfig
            {
                Enabled = true,
                MaxConsensusAttempts = 1,
                MaxConsensusConcurrency = 2
            },
            doc,
            personas);
        var ui = new BlazorUserInterface();
        var conversation = new AgentConversationOrchestrator(
            ui,
            personas,
            doc,
            new AppSettings
            {
                MaxConversationTurns = 50,
                Orchestrator = new OrchestratorConfig { MaxConsensusConcurrency = 2 }
            },
            orchestrator);

        try
        {
            await conversation.StartConversationAsync("Test topic");

            // With 5 personas and a concurrency cap of 2, at most 2 consensus
            // assessments may ever be in flight simultaneously.
            Assert.Equal(2, tracker.MaxObservedConcurrency);
            Assert.True(tracker.TotalConsensusCalls >= 5,
                "Every persona should participate in the consensus check.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OrchestratedConversation_RespectsDefaultConsensusBudgetAndConcurrency()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-consensus-tests", Guid.NewGuid().ToString("N"));
        var doc = new CollaborativeMarkdownDocument(new ApplicationDataPathResolver(root));
        var tracker = new ConsensusConcurrencyTracker();
        var agents = Enumerable.Range(0, 4)
            .Select(i => new ConcurrencyTrackingAgent("CONSENSUS: NO\nReason: Still deliberating.", tracker))
            .ToList();
        var personas = agents.Select((agent, i) => new AgentPersona(
            agent,
            new PersonaConfig { Name = $"Persona{i}", SystemPrompt = $"You are Persona{i}." },
            doc,
            null,
            maxTurns: 1,
            agentCount: 4,
            new RetryService(null))).ToList();
        var orchestrator = new AgentOrchestrator(
            new TestAIAgent("Conclude\nReason: ready to finish"),
            new OrchestratorConfig { Enabled = true },
            doc,
            personas);
        var ui = new BlazorUserInterface();
        var conversation = new AgentConversationOrchestrator(
            ui,
            personas,
            doc,
            new AppSettings { MaxConversationTurns = 50 },
            orchestrator);

        try
        {
            await conversation.StartConversationAsync("Test topic");

            // Defaults apply when nothing is configured: 2 consensus attempts and
            // concurrency capped at 2 (so 4 personas are evaluated in 2 + 2 waves).
            Assert.Equal(ConversationTerminationReason.ConsensusBudgetExhausted, ui.TerminationReason);
            Assert.Equal(2, tracker.MaxObservedConcurrency);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

/// <summary>
/// Shared counter for in-flight consensus calls across all persona agents, used to
/// assert the orchestrator's consensus concurrency cap.
/// </summary>
internal sealed class ConsensusConcurrencyTracker
{
    private int _inFlight;
    private int _maxObserved;
    private int _totalCalls;
    public int MaxObservedConcurrency => _maxObserved;
    public int TotalConsensusCalls => _totalCalls;

    public async Task<T> TrackAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var concurrent = Interlocked.Increment(ref _inFlight);
        InterlockedMax(ref _maxObserved, concurrent);
        Interlocked.Increment(ref _totalCalls);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (value <= current || Interlocked.CompareExchange(ref target, value, current) == current)
                return;
        }
    }
}

/// <summary>
/// AIAgent double that routes every call through a shared <see cref="ConsensusConcurrencyTracker"/>
/// so tests can observe the aggregate concurrency of consensus assessments.
/// </summary>
internal sealed class ConcurrencyTrackingAgent : AIAgent
{
    private readonly string _response;
    private readonly ConsensusConcurrencyTracker _tracker;

    public ConcurrencyTrackingAgent(string response, ConsensusConcurrencyTracker tracker)
    {
        _response = response;
        _tracker = tracker;
    }

    public override AgentThread GetNewThread() => throw new NotSupportedException();
    public override AgentThread DeserializeThread(JsonElement serializedThread, JsonSerializerOptions? jsonSerializerOptions = null)
        => throw new NotSupportedException();

    public override Task<AgentRunResponse> RunAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Give the semaphore a chance to serialize overly-parallel invocations.
        return _tracker.TrackAsync(async token =>
        {
            await Task.Delay(20, token);
            return new AgentRunResponse(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, _response));
        }, cancellationToken);
    }

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await RunAsync(messages, thread, options, cancellationToken);
        yield return new AgentRunResponseUpdate(new Microsoft.Extensions.AI.ChatResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, response.ToString()));
    }
}
