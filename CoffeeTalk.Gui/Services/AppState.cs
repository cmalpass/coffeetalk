using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Gui.Services;

public class AppState
{
    private readonly ConfigurationService _configService;

    public AppSettings Settings { get; private set; } = new();

    public event Action? OnChange;

    public AppState(ConfigurationService configService)
    {
        _configService = configService;
        LoadSettings();
    }

    public void LoadSettings()
    {
        Settings = _configService.LoadConfiguration();
        NotifyStateChanged();
    }

    public async Task SaveSettingsAsync()
    {
        await _configService.SaveSettingsAsync(Settings);
        NotifyStateChanged();
    }

    public void AddPersona(PersonaConfig persona)
    {
        Settings.Personas.Add(persona);
        NotifyStateChanged();
    }

    public void UpdatePersona(PersonaConfig existingPersona, PersonaConfig updatedPersona)
    {
        var index = Settings.Personas.IndexOf(existingPersona);
        if (index < 0)
        {
            return;
        }

        Settings.Personas[index] = updatedPersona;
        NotifyStateChanged();
    }

    public void RemovePersona(PersonaConfig persona)
    {
        if (Settings.Personas.Remove(persona))
        {
            NotifyStateChanged();
        }
    }

    public void NotifyStateChanged() => OnChange?.Invoke();
}
