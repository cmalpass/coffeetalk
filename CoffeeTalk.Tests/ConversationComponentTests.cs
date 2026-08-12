using Xunit;
using Bunit;
using CoffeeTalk.Gui.Components;
using CoffeeTalk.Gui.Services;
using CoffeeTalk.Services;
using CoffeeTalk.Core.Interfaces;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Moq;
using Bunit.Rendering;

namespace CoffeeTalk.Tests;

public class ConversationComponentTests : TestContext
{
    private static (BlazorUserInterface Ui, IRenderedComponent<ContainerFragment> Component) RenderConversation(TestContext context)
    {
        context.Services.AddMudServices();
        context.Services.AddSingleton<ConfigurationService>();
        context.Services.AddSingleton<AppState>();
        context.Services.AddSingleton<IApplicationDataPathResolver>(
            new ApplicationDataPathResolver(Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"))));
        context.Services.AddSingleton<MudBlazor.ISnackbar, MudBlazor.SnackbarService>();
        context.Services.AddSingleton<IPdfDocumentExporter, PdfDocumentExporter>();
        context.Services.AddSingleton<IMemoryStoreService>(new Mock<IMemoryStoreService>().Object);

        var ui = new BlazorUserInterface();
        context.Services.AddSingleton(ui);
        context.Services.AddSingleton<IConversationSessionService>(new Mock<IConversationSessionService>().Object);

        var component = context.Render(builder =>
        {
            builder.OpenComponent<Conversation>(0);
            builder.CloseComponent();
        });
        return (ui, component);
    }

    [Fact]
    public void Conversation_ShouldRenderMessages()
    {
        // Arrange
        var (ui, cut) = RenderConversation(this);

        ui.Messages.Add(new ChatMessage { Sender = "Agent", Content = "Hello" });
        ui.NotifyStateChanged();

        cut.WaitForAssertion(() => Assert.Contains("Hello", cut.Markup));

        // Assert
        Assert.Contains("Agent", cut.Markup);
    }

    [Fact]
    public void Conversation_ShouldHandleStateChangesWithOneRendererSafeScroll()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var (ui, cut) = RenderConversation(this);

        ui.NotifyStateChanged();

        cut.WaitForAssertion(() =>
            Assert.Single(JSInterop.Invocations.Where(invocation => invocation.Identifier == "scrollToBottom")));
    }

    [Fact]
    public void Conversation_ShouldStopHandlingStateChangesAfterDisposal()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var (ui, cut) = RenderConversation(this);
        ui.NotifyStateChanged();
        cut.WaitForAssertion(() =>
            Assert.Single(JSInterop.Invocations.Where(invocation => invocation.Identifier == "scrollToBottom")));
        var scrollInvocation = JSInterop.Invocations.Single(invocation => invocation.Identifier == "scrollToBottom");

        cut.FindComponent<Conversation>().Instance.Dispose();
        ui.NotifyStateChanged();

        Assert.True(scrollInvocation.CancellationToken?.IsCancellationRequested);
        Assert.Single(JSInterop.Invocations.Where(invocation => invocation.Identifier == "scrollToBottom"));
    }
}
