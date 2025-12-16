using Xunit;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Services;
using CoffeeTalk.Models;
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
}
