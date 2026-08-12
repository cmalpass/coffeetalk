using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Gui.Services;

public class AppState
{
    private readonly ConfigurationService _configService;
    private readonly WorkspaceService? _workspaceService;
    private readonly ApplicationDataPathResolver? _dataPaths;

    public AppSettings Settings { get; private set; } = new();
    public WorkspaceMetadata? CurrentWorkspace => _workspaceService?.Active;

    public event Action? OnChange;

    public AppState(ConfigurationService configService, WorkspaceService? workspaceService = null,
        ApplicationDataPathResolver? dataPaths = null)
    {
        _configService = configService;
        _workspaceService = workspaceService;
        _dataPaths = dataPaths;
        LoadSettings();
    }

    public Task<IReadOnlyList<WorkspaceMetadata>> ListWorkspacesAsync()
        => _workspaceService?.ListAsync() ?? Task.FromResult<IReadOnlyList<WorkspaceMetadata>>(Array.Empty<WorkspaceMetadata>());

    public async Task SwitchWorkspaceAsync(string id)
    {
        if (_workspaceService is null || _dataPaths is null)
            throw new InvalidOperationException("Workspace management is unavailable.");
        await _workspaceService.SwitchAsync(id);
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
