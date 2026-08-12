using CoffeeTalk.Models;
using CoffeeTalk.Services;
using Microsoft.Agents.AI;
using Moq;

namespace CoffeeTalk.Tests;

public sealed class AgentEditorTests
{
    [Fact]
    public async Task ReviewAndEditAsync_ReturnsExplicitEmptyDocumentResultWithoutCallingAgent()
    {
        var agent = new Mock<AIAgent>(MockBehavior.Strict);
        var editor = new AgentEditor(
            agent.Object,
            new EditorConfig(),
            new CollaborativeMarkdownDocument(),
            null);

        var result = await editor.ReviewAndEditAsync("context");

        Assert.Equal("Document is empty - nothing to edit yet.", result);
        agent.VerifyNoOtherCalls();
    }
}
