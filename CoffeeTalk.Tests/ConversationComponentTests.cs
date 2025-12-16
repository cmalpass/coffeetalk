using Xunit;
using Bunit;
using CoffeeTalk.Gui.Components;
using CoffeeTalk.Gui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeTalk.Tests;

public class ConversationComponentTests : TestContext
{
    [Fact]
    public void Conversation_ShouldRenderMessages()
    {
        // Arrange
        var ui = new BlazorUserInterface();
        Services.AddSingleton<BlazorUserInterface>(ui);

        ui.Messages.Add(new ChatMessage { Sender = "Agent", Content = "Hello" });

        // Act
        // The previous attempt failed because I wrapped the method call in pragma but the using directive
        // might have been causing issues or it wasn't suppressed correctly.
        // Also BunitContext is the new recommended base class.

        // I'll assume the obsolete error is hard (treated as error).
        // If I can't use Razor syntax in .cs file easily without setup, and RenderComponent is obsolete/error,
        // I might need to use a RenderComponent that accepts type or a factory if available,
        // OR fix the Razor syntax support.

        // However, standard bUnit tests in .cs SHOULD allow RenderComponent<T>().
        // The error suggests using `Render` instead.
        // Render usually takes a RenderFragment.
        // RenderFragment can be created programmatically if Razor syntax fails.

        var cut = Render(builder => {
            builder.OpenComponent<Conversation>(0);
            builder.CloseComponent();
        });

        // Assert
        var message = cut.Find(".message.agent");
        Assert.NotNull(message);

        var header = message.QuerySelector(".message-header");
        Assert.Equal("Agent", header?.TextContent);

        var content = message.QuerySelector(".message-content");
        Assert.Equal("Hello", content?.TextContent);
    }
}
