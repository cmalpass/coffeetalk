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
}
