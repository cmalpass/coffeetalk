using Microsoft.Extensions.Configuration;
using CoffeeTalk.Models;
using System.Text.Json;

namespace CoffeeTalk.Services;

public class ConfigurationService
{
    private const string SettingsFile = "appsettings.json";
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly IApplicationDataPathResolver _paths;

    public ConfigurationService(IApplicationDataPathResolver? paths = null)
    {
        _paths = paths ?? new ApplicationDataPathResolver();
    }

    public AppSettings LoadConfiguration()
    {
        var settingsPath = File.Exists(_paths.ConfigurationFilePath)
            ? _paths.ConfigurationFilePath
            : FindLegacyConfigurationPath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(settingsPath)!)
            .AddJsonFile(Path.GetFileName(settingsPath), optional: true, reloadOnChange: false)
            .Build();

        var settings = new AppSettings();
        configuration.Bind(settings);
        if (string.IsNullOrWhiteSpace(settings.LlmProvider.ApiKey))
        {
            settings.LlmProvider.ApiKey = settings.LlmProvider.Type.ToLowerInvariant() switch
            {
                "openai" => Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty,
                "azureopenai" => Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? string.Empty,
                _ => string.Empty
            };
        }
        if (settings.LlmProvider.Type.Equals("azureopenai", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(settings.LlmProvider.Endpoint))
                settings.LlmProvider.Endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(settings.LlmProvider.DeploymentName))
            {
                settings.LlmProvider.DeploymentName =
                    Environment.GetEnvironmentVariable("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME") ??
                    Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ??
                    Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
            }
        }

        return settings;
    }

    // Moved interactive configuration logic to a separate service/handler or interface in the CLI layer
    // because it depends heavily on UI interactions (AnsiConsole).
    // The core ConfigurationService should just handle loading/saving.

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var persistedSettings = MapToPersistedAppSettings(settings);
        var json = JsonSerializer.Serialize(persistedSettings, _jsonOptions);

        Directory.CreateDirectory(_paths.RootDirectory);
        await File.WriteAllTextAsync(_paths.ConfigurationFilePath, json);
    }

    private static string FindLegacyConfigurationPath()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, SettingsFile);
        if (File.Exists(basePath))
            return basePath;

        var currentPath = Path.GetFullPath(SettingsFile);
        return currentPath;
    }

    // Helper method to map AppSettings to PersistedAppSettings, omitting sensitive fields
    private static PersistedAppSettings MapToPersistedAppSettings(AppSettings settings)
    {
        return new PersistedAppSettings
        {
            LlmProvider = new PersistedLlmProviderConfig
            {
                Type = settings.LlmProvider.Type,
                Endpoint = settings.LlmProvider.Endpoint,
                ModelId = settings.LlmProvider.ModelId,
                DeploymentName = settings.LlmProvider.DeploymentName ?? string.Empty,
                StreamingEnabled = settings.LlmProvider.StreamingEnabled,
                StreamingSupported = settings.LlmProvider.StreamingSupported,
                StreamingFallback = settings.LlmProvider.StreamingFallback
                // ApiKey is intentionally omitted
            },
            Personas = settings.Personas,
            MaxConversationTurns = settings.MaxConversationTurns,
            ShowThinking = settings.ShowThinking,
            IncludeThinkingInTelemetry = settings.IncludeThinkingInTelemetry,
            InteractiveMode = settings.InteractiveMode,
            DevilsAdvocate = settings.DevilsAdvocate,
            ContextSummarization = settings.ContextSummarization,
            StructuredData = settings.StructuredData,
            FactChecking = settings.FactChecking,
            RateLimit = settings.RateLimit,
            Retry = settings.Retry,
            Orchestrator = settings.Orchestrator,
            Editor = settings.Editor,
            DynamicPersonas = settings.DynamicPersonas,
            Tools = settings.Tools,
            Memory = settings.Memory
        };
    }
}
