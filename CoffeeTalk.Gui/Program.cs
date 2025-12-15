using Photino.Blazor;
using Microsoft.Extensions.DependencyInjection;
using CoffeeTalk.Services;
using CoffeeTalk.Gui.Services;
using CoffeeTalk.Core.Interfaces;
// using CoffeeTalk.Gui.Components; // It seems this is problematic.

namespace CoffeeTalk.Gui;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

        appBuilder.Services.AddLogging();
        appBuilder.Services.AddSingleton<ConfigurationService>();

        // Register the UI as a singleton so it can be shared between the background task and the pages
        appBuilder.Services.AddSingleton<BlazorUserInterface>();
        appBuilder.Services.AddSingleton<IUserInterface>(sp => sp.GetRequiredService<BlazorUserInterface>());

        // Try using the type directly if possible, or fully qualified.
        // Since App is in CoffeeTalk.Gui.Components namespace.
        appBuilder.RootComponents.Add<CoffeeTalk.Gui.Components.App>("#app");

        var app = appBuilder.Build();

        app.MainWindow
            .SetTitle("CoffeeTalk GUI")
            .SetUseOsDefaultSize(false)
            .SetSize(1024, 768)
            .SetIconFile("favicon.ico"); // Optional

        app.Run();
    }
}
