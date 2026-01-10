using Xunit;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Services;
using CoffeeTalk.Models;
using Microsoft.Agents.AI;
using System.Threading.Tasks;
using System.Collections.Generic;
using Moq;
using System.Reflection;

namespace CoffeeTalk.Tests;

public class AgentOrchestratorTests
{
    [Fact]
    public void BuildOrchestratorContext_ShouldCorrectlySortSpeakerStats()
    {
        // Arrange
        var mockAgent = new Mock<AIAgent>();
        var config = new OrchestratorConfig();
        var doc = new CollaborativeMarkdownDocument();

        var personaConfigA = new PersonaConfig { Name = "Alice", SystemPrompt = "You are Alice." };
        var personaConfigB = new PersonaConfig { Name = "Bob", SystemPrompt = "You are Bob." };
        var personaConfigC = new PersonaConfig { Name = "Charlie", SystemPrompt = "You are Charlie." };

        var personaA = new AgentPersona(mockAgent.Object, personaConfigA, doc, null, 10, 3);
        var personaB = new AgentPersona(mockAgent.Object, personaConfigB, doc, null, 10, 3);
        var personaC = new AgentPersona(mockAgent.Object, personaConfigC, doc, null, 10, 3);

        var personas = new List<AgentPersona> { personaA, personaB, personaC };

        var orchestrator = new AgentOrchestrator(mockAgent.Object, config, doc, personas);

        // Access private fields/methods using Reflection to simulate usage
        var speakerCountField = typeof(AgentOrchestrator).GetField("_speakerCount", BindingFlags.NonPublic | BindingFlags.Instance);
        var speakerCount = (Dictionary<string, int>)speakerCountField.GetValue(orchestrator);

        // Set up counts: Bob=5, Alice=10, Charlie=2
        // Sorted order should be: Charlie (2), Bob (5), Alice (10)
        speakerCount["Alice"] = 10;
        speakerCount["Bob"] = 5;
        speakerCount["Charlie"] = 2;

        // Since we introduced a cache, we must trigger the update method manually
        // because we are modifying the dictionary via reflection, bypassing the normal logic.
        var updateMethod = typeof(AgentOrchestrator).GetMethod("UpdateCachedSpeakerStats", BindingFlags.NonPublic | BindingFlags.Instance);
        updateMethod.Invoke(orchestrator, null);

        var buildContextMethod = typeof(AgentOrchestrator).GetMethod("BuildOrchestratorContext", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        string context = (string)buildContextMethod.Invoke(orchestrator, new object[] { "Test message", new List<string>(), 10 });

        // Assert
        Assert.Contains("- Charlie: 2 time(s)", context);
        Assert.Contains("- Bob: 5 time(s)", context);
        Assert.Contains("- Alice: 10 time(s)", context);

        // Check order
        int indexCharlie = context.IndexOf("- Charlie: 2 time(s)");
        int indexBob = context.IndexOf("- Bob: 5 time(s)");
        int indexAlice = context.IndexOf("- Alice: 10 time(s)");

        Assert.True(indexCharlie < indexBob, "Charlie should come before Bob");
        Assert.True(indexBob < indexAlice, "Bob should come before Alice");
    }
}
