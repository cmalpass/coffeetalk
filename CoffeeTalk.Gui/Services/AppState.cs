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

    private void NotifyStateChanged() => OnChange?.Invoke();
}
