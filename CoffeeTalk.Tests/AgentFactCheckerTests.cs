using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class AgentFactCheckerTests
{
    [Fact]
    public async Task CheckAsync_SkipsShortMessages()
    {
        var agent = new TestAIAgent("unused");
        var checker = new AgentFactChecker(agent, null);

        await checker.CheckAsync("too short");

        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task CheckAsync_RaisesAlertForFlaggedClaims()
    {
        var checker = new AgentFactChecker(
            new TestAIAgent("FLAG: verify this claim"),
            null);
        string? alert = null;
        checker.OnFactCheckAlert += message =>
        {
            alert = message;
            return Task.CompletedTask;
        };

        await checker.CheckAsync("This is a sufficiently long statement to verify.");

        Assert.Equal("FLAG: verify this claim", alert);
    }
}
