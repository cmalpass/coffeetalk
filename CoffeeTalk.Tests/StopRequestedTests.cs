using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;
using CoffeeTalk.Services;
using Microsoft.Agents.AI;
using Moq;

namespace CoffeeTalk.Tests;

public class StopRequestedTests
{
    private static AgentPersona CreatePersona(string name) =>
        new(
            new TestAIAgent("A brief response."),
            new PersonaConfig { Name = name, SystemPrompt = $"You are {name}." },
            new CollaborativeMarkdownDocument(),
            null,
            maxTurns: 1,
            agentCount: 1);

    [Fact]
    public async Task RoundRobin_StopRequested_SetsUserStoppedTerminationReason()
    {
        // Arrange: StopRequested is already true so the loop stops before any turn.
        var ui = new Mock<IUserInterface>();
        ui.SetupProperty(u => u.TerminationReason, ConversationTerminationReason.Unknown);
        ui.Setup(u => u.StopRequested).Returns(true);

        var orchestrator = new AgentConversationOrchestrator(
            ui.Object,
            new List<AgentPersona> { CreatePersona("Analyst"), CreatePersona("Critic") },
            new CollaborativeMarkdownDocument(),
            new AppSettings { MaxConversationTurns = 5 });

        // Act
        await orchestrator.StartConversationAsync("Test topic");

        // Assert
        Assert.Equal(ConversationTerminationReason.UserStopped, ui.Object.TerminationReason);
        // The conversation header is still shown, then it returns without running persona turns.
        ui.Verify(u => u.ShowConversationHeaderAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }
}
