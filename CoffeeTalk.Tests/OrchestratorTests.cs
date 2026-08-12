using Xunit;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Services;
using CoffeeTalk.Models;
using CoffeeTalk.Gui.Services;
using Microsoft.Agents.AI;
using System.Threading.Tasks;
using System.Collections.Generic;
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
}
