using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class AgentDataExtractorTests
{
    [Fact]
    public void BuildSystemPrompt_ContainsConfiguredSchemaAndJsonOnlyInstruction()
    {
        var prompt = AgentDataExtractor.BuildSystemPrompt(
            new StructuredDataConfig { SchemaDescription = "a contact record" });

        Assert.Contains("a contact record", prompt);
        Assert.Contains("ONLY valid JSON", prompt);
    }

    [Fact]
    public async Task ExtractAndSaveAsync_PropagatesCancellationBeforeAgentCall()
    {
        var agent = new TestAIAgent("unused");
        var extractor = new AgentDataExtractor(
            agent,
            new StructuredDataConfig(),
            new CollaborativeMarkdownDocument());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            extractor.ExtractAndSaveAsync(new List<string>(), cancellation.Token));
    }

    [Fact]
    public async Task ExtractAndSaveAsync_WritesValidJsonInsideCodeFence()
    {
        using var workspace = new TemporaryDataWorkspace();
        var events = new RecordingEventSink();
        var agent = new TestAIAgent("```json\n{\"name\": \"Ada\", \"role\": \"analyst\"}\n```");
        var extractor = new AgentDataExtractor(
            agent,
            new StructuredDataConfig(),
            new CollaborativeMarkdownDocument(),
            new RetryService(null),
            events,
            new ApplicationDataPathResolver(workspace.Root));

        await extractor.ExtractAndSaveAsync([]);

        var outputPath = workspace.ResolveDataPath("data.json");
        Assert.True(File.Exists(outputPath));
        Assert.Equal("{\"name\": \"Ada\", \"role\": \"analyst\"}", await File.ReadAllTextAsync(outputPath));
        Assert.DoesNotContain(events.Events, item => item.Kind == OperationalEventKind.DataExtractionFailed);
    }

    [Fact]
    public async Task ExtractAndSaveAsync_MalformedJsonIsRejected_RetriesOnce_ThenWritesValidOutput()
    {
        using var workspace = new TemporaryDataWorkspace();
        var events = new RecordingEventSink();
        var call = 0;
        var agent = new TestAIAgent(() =>
        {
            call++;
            return new AgentRunResponse(
                new ChatMessage(ChatRole.Assistant, call == 1 ? "some prose, not json" : "{\"ok\": true}"));
        });
        var extractor = new AgentDataExtractor(
            agent,
            new StructuredDataConfig(),
            new CollaborativeMarkdownDocument(),
            new RetryService(null),
            events,
            new ApplicationDataPathResolver(workspace.Root));

        await extractor.ExtractAndSaveAsync([]);

        var outputPath = workspace.ResolveDataPath("data.json");
        Assert.True(File.Exists(outputPath));
        Assert.Equal("{\"ok\": true}", await File.ReadAllTextAsync(outputPath));
        Assert.Contains(events.Events, item => item.Kind == OperationalEventKind.DataExtractionRetry);
        Assert.DoesNotContain(events.Events, item => item.Kind == OperationalEventKind.DataExtractionFailed);
        Assert.Equal(2, agent.Calls);
    }

    [Fact]
    public async Task ExtractAndSaveAsync_MalformedJsonExhaustsBudget_DoesNotWriteFile_AndSurfacesFailure()
    {
        using var workspace = new TemporaryDataWorkspace();
        var events = new RecordingEventSink();
        var agent = new TestAIAgent("this is definitely not JSON");
        var extractor = new AgentDataExtractor(
            agent,
            new StructuredDataConfig(),
            new CollaborativeMarkdownDocument(),
            new RetryService(null),
            events,
            new ApplicationDataPathResolver(workspace.Root));

        await extractor.ExtractAndSaveAsync([]);

        var outputPath = workspace.ResolveDataPath("data.json");
        Assert.False(File.Exists(outputPath));
        Assert.Contains(events.Events, item => item.Kind == OperationalEventKind.DataExtractionFailed);
        Assert.Equal(2, agent.Calls);
    }

    private sealed class RecordingEventSink : IOperationalEventSink
    {
        public List<OperationalEvent> Events { get; } = [];

        public void Publish(OperationalEvent operationalEvent) => Events.Add(operationalEvent);
    }

    private sealed class TemporaryDataWorkspace : IDisposable
    {
        public TemporaryDataWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "CoffeeTalk.Tests", Guid.NewGuid().ToString("N"));
        }

        public string Root { get; }

        public string ResolveDataPath(string fileName) => Path.Combine(Root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
