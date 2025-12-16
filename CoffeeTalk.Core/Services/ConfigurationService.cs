using Microsoft.Extensions.Configuration;
using CoffeeTalk.Models;
using System.Text.Json;

namespace CoffeeTalk.Services;

public class ConfigurationService
{
    private const string SettingsFile = "appsettings.json";

    public AppSettings LoadConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(SettingsFile, optional: true, reloadOnChange: false)
            .Build();

        var settings = new AppSettings();
        configuration.Bind(settings);

        return settings;
    }

    // Moved interactive configuration logic to a separate service/handler or interface in the CLI layer
    // because it depends heavily on UI interactions (AnsiConsole).
    // The core ConfigurationService should just handle loading/saving.

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var persistedSettings = MapToPersistedAppSettings(settings);
        var json = JsonSerializer.Serialize(persistedSettings, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(SettingsFile, json);
    }

    // Helper method to map AppSettings to PersistedAppSettings, omitting sensitive fields
    private PersistedAppSettings MapToPersistedAppSettings(AppSettings settings)
    {
        return new PersistedAppSettings
        {
            LlmProvider = new PersistedLlmProviderConfig
            {
                Type = settings.LlmProvider.Type,
                Endpoint = settings.LlmProvider.Endpoint,
                ModelId = settings.LlmProvider.ModelId,
                DeploymentName = settings.LlmProvider.DeploymentName
                // ApiKey is intentionally omitted
            },
            Personas = settings.Personas,
            MaxConversationTurns = settings.MaxConversationTurns,
            ShowThinking = settings.ShowThinking,
            InteractiveMode = settings.InteractiveMode,
            DevilsAdvocate = settings.DevilsAdvocate,
            ContextSummarization = settings.ContextSummarization,
            StructuredData = settings.StructuredData,
            FactChecking = settings.FactChecking,
            RateLimit = settings.RateLimit,
            Retry = settings.Retry,
            Orchestrator = settings.Orchestrator,
            Editor = settings.Editor,
            DynamicPersonas = settings.DynamicPersonas
        };
    }
}
