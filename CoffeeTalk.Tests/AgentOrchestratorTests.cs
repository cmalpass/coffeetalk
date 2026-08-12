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
}
