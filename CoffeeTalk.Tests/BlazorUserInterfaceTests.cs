using Xunit;
using CoffeeTalk.Gui.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeTalk.Tests;

public class BlazorUserInterfaceTests
{
    [Fact]
    public async Task ShowMessageAsync_ShouldAddMessage_AndNotifyChange()
    {
        // Arrange
        var ui = new BlazorUserInterface();
        bool notified = false;
        ui.OnChange += () => notified = true;

        // Act
        await ui.ShowMessageAsync("Hello World");

        // Assert
        Assert.True(notified);
        Assert.Single(ui.Messages);
        Assert.Equal("Hello World", ui.Messages[0].Content);
        Assert.True(ui.Messages[0].IsSystem);
    }

    [Fact]
    public async Task GetUserInterventionAsync_ShouldPause_AndResumeOnSubmit()
    {
        // Arrange
        var ui = new BlazorUserInterface();

        // Act - Start intervention
        var task = ui.GetUserInterventionAsync();

        // Assert state
        Assert.True(ui.IsInterventionRequired);
        Assert.False(task.IsCompleted);

        // Act - Submit
        ui.SubmitIntervention("continue", "ok");

        // Assert result
        var result = await task;
        Assert.Equal("continue", result.Action);
        Assert.Equal("ok", result.Message);
        Assert.False(ui.IsInterventionRequired);
    }

    [Fact]
    public async Task GetUserInterventionAsync_ShouldReusePendingIntervention()
    {
        var ui = new BlazorUserInterface();

        var first = ui.GetUserInterventionAsync();
        var second = ui.GetUserInterventionAsync();

        Assert.Same(first, second);
        ui.SubmitIntervention("continue", "ok");
        await first;
    }

    [Fact]
    public async Task CancelIntervention_ShouldCompletePendingTaskAsCanceled()
    {
        var ui = new BlazorUserInterface();
        var intervention = ui.GetUserInterventionAsync();

        ui.CancelIntervention();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => intervention);
        Assert.False(ui.IsInterventionRequired);
        Assert.True(ui.StopRequested);
    }

    [Fact]
    public async Task ResetForNewConversation_ShouldClearConversationScopedState()
    {
        var ui = new BlazorUserInterface();
        await ui.ShowConversationHeaderAsync("first", new[] { "A", "B" }, "Mode", false);
        await ui.ShowAgentResponseAsync("A", "old message");
        await ui.ShowDocumentPreviewAsync("# old");

        ui.ResetForNewConversation();

        Assert.Empty(ui.Messages);
        Assert.Null(ui.ConversationTopic);
        Assert.Empty(ui.ConversationParticipants);
        Assert.Empty(ui.DocumentMarkdown);
        Assert.False(ui.IsConversationRunning);
    }

    [Fact]
    public async Task LoadConversation_ShouldPreserveHistoryState()
    {
        var ui = new BlazorUserInterface();
        var record = new ConversationRecord
        {
            Topic = "history",
            Personas = new List<string> { "A", "B" },
            Messages = new List<ChatMessage> { new() { Sender = "A", Content = "saved" } }
        };

        ui.LoadConversation(record);

        Assert.Equal("history", ui.ConversationTopic);
        Assert.Single(ui.Messages);
        Assert.Equal("saved", ui.Messages[0].Content);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task EndConversation_CancelsPendingIntervention()
    {
        var ui = new BlazorUserInterface();
        var intervention = ui.GetUserInterventionAsync();

        ui.EndConversation();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => intervention);
        Assert.False(ui.IsConversationRunning);
    }
}
