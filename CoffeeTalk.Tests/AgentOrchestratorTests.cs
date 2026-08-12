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
}
