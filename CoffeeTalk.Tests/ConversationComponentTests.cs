using Xunit;
using Bunit;
using CoffeeTalk.Gui.Components.Pages; // This is the correct namespace, but sometimes Razor class generation is tricky
using CoffeeTalk.Gui.Components; // Trying this just in case
using CoffeeTalk.Gui.Services;
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

        var ui = new BlazorUserInterface();
        Services.AddSingleton<BlazorUserInterface>(ui);

        ui.Messages.Add(new ChatMessage { Sender = "Agent", Content = "Hello" });

        // Act
        // Use Render fragment as fallback since generic RenderComponent might not find the type if there are namespace issues
        var cut = Render(builder => {
             builder.OpenComponent<Conversation>(0);
             builder.CloseComponent();
        });

        // Assert
        // MudBlazor components render differently.
        // We verify text content.

        Assert.Contains("Hello", cut.Markup);
        Assert.Contains("Agent", cut.Markup);
    }
}
