using Xunit;
using Bunit;
using CoffeeTalk.Gui.Components;
using CoffeeTalk.Gui.Services;
using CoffeeTalk.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace CoffeeTalk.Tests;

public class ConversationComponentTests : TestContext
{
    [Fact]
    public void Conversation_ShouldRenderMessages()
    {
        // Arrange
        Services.AddMudServices();
        Services.AddSingleton<ConfigurationService>();
        Services.AddSingleton<AppState>();
        Services.AddSingleton<MudBlazor.ISnackbar, MudBlazor.SnackbarService>();

        var ui = new BlazorUserInterface();
        Services.AddSingleton<BlazorUserInterface>(ui);

        ui.Messages.Add(new ChatMessage { Sender = "Agent", Content = "Hello" });

        // Act
        // Trying to use Render instead of RenderComponent to avoid obsolete error
        var cut = Render(builder => {
            builder.OpenComponent<Conversation>(0);
            builder.CloseComponent();
        });

        // Assert
        Assert.Contains("Hello", cut.Markup);
        Assert.Contains("Agent", cut.Markup);
    }
}
