using CoffeeTalk.Gui.Services;
using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class AppStateTests
{
    [Fact]
    public void PersonaMutations_NotifyExactlyOncePerMutation()
    {
        var appState = CreateAppState();
        var notifications = 0;
        appState.OnChange += () => notifications++;

        var addedPersona = new PersonaConfig { Name = "Added" };
        appState.AddPersona(addedPersona);
        Assert.Equal(1, notifications);

        var updatedPersona = new PersonaConfig { Name = "Updated" };
        appState.UpdatePersona(addedPersona, updatedPersona);
        Assert.Equal(2, notifications);

        appState.RemovePersona(updatedPersona);
        Assert.Equal(3, notifications);
    }

    [Fact]
    public void CanceledOrMissingPersonaMutations_DoNotNotify()
    {
        var appState = CreateAppState();
        var notifications = 0;
        appState.OnChange += () => notifications++;

        appState.UpdatePersona(new PersonaConfig(), new PersonaConfig { Name = "Updated" });
        appState.RemovePersona(new PersonaConfig());

        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task SaveAndReload_NotifyOncePerOperation()
    {
        var appState = CreateAppState();
        var notifications = 0;
        appState.OnChange += () => notifications++;

        appState.AddPersona(new PersonaConfig
        {
            Name = "Persisted",
            SystemPrompt = "A persisted persona"
        });
        await appState.SaveSettingsAsync();
        Assert.Equal(2, notifications);

        appState.LoadSettings();
        Assert.Equal(3, notifications);
        var persona = Assert.Single(appState.Settings.Personas);
        Assert.Equal("Persisted", persona.Name);
        Assert.Equal("A persisted persona", persona.SystemPrompt);
    }

    private static AppState CreateAppState()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
        var resolver = new ApplicationDataPathResolver(root);
        return new AppState(new ConfigurationService(resolver));
    }
}
