using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class AgentOrchestratorTests
{
    [Fact]
    public void BuildSystemPrompt_ListsPersonasAndDecisionFormat()
    {
        var prompt = AgentOrchestrator.BuildSystemPrompt(
            new OrchestratorConfig { BaseSystemPrompt = "Base instructions" },
            new List<AgentPersona>());

        Assert.Contains("Base instructions", prompt);
        Assert.Contains("CONCLUDE", prompt);
        Assert.Contains("Line 1", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ListsPersonaCapabilities()
    {
        var persona = new AgentPersona(
            new TestAIAgent(() => throw new InvalidOperationException()),
            new PersonaConfig { Name = "Architect", SystemPrompt = "You are Architect." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 2,
            agentCount: 1,
            retryService: new RetryService(null),
            effectiveToolNames: ["AddHeading"]);

        var prompt = AgentOrchestrator.BuildSystemPrompt(
            new OrchestratorConfig { BaseSystemPrompt = "Base instructions" },
            [persona]);

        Assert.Contains("Capabilities: AddHeading", prompt);
    }

    [Fact]
    public async Task SelectNextSpeakerAsync_UsesBoundedStatelessContext()
    {
        var agent = new TestAIAgent("Architect\nReason: address the document");
        var document = new CollaborativeMarkdownDocument();
        document.AppendParagraph(new string('x', AgentContextPolicy.MaxDocumentCharacters * 2));
        var persona = new AgentPersona(
            new TestAIAgent("response"),
            new PersonaConfig { Name = "Architect", SystemPrompt = "You are Architect." },
            document,
            null,
            maxTurns: 2,
            agentCount: 1);
        var orchestrator = new AgentOrchestrator(
            agent,
            new OrchestratorConfig(),
            document,
            [persona]);

        var selected = await orchestrator.SelectNextSpeakerAsync("topic", ["first", "second"], turnsRemaining: 1);

        Assert.Same(persona, selected);
        Assert.Single(agent.Prompts);
        Assert.InRange(agent.Prompts[0].Length, 1, AgentContextPolicy.MaxPromptCharacters);
        Assert.Contains("first", agent.Prompts[0]);
        Assert.Contains("second", agent.Prompts[0]);
    }
}
