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
}
