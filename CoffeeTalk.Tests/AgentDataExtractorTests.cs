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
}
