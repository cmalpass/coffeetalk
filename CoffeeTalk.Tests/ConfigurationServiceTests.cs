using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public async Task SaveAndLoad_UsesInjectedAbsolutePathAndPersistsTools()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
        var resolver = new ApplicationDataPathResolver(root);
        var service = new ConfigurationService(resolver);
        var settings = new AppSettings
        {
            LlmProvider = new LlmProviderConfig { Type = "ollama", ModelId = "test-model" },
            Tools = new ToolsConfig { EnableFallbackJsonTools = false, RequireToolsVerification = false },
            Memory = new MemoryConfig { Enabled = true, MaxEntries = 7 }
        };

        await service.SaveSettingsAsync(settings);
        var loaded = service.LoadConfiguration();

        Assert.True(Path.IsPathFullyQualified(resolver.ConfigurationFilePath));
        Assert.Equal(false, loaded.Tools?.EnableFallbackJsonTools);
        Assert.Equal(false, loaded.Tools?.RequireToolsVerification);
        Assert.True(loaded.Memory.Enabled);
        Assert.Equal(7, loaded.Memory.MaxEntries);
    }

    [Fact]
    public async Task SaveAndLoad_AllowsPartialNullableSettings()
    {
        var resolver = new ApplicationDataPathResolver(
            Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N")));
        var service = new ConfigurationService(resolver);

        await service.SaveSettingsAsync(new AppSettings
        {
            RateLimit = null,
            Tools = null,
            Orchestrator = null,
            Editor = null,
            DynamicPersonas = null,
            StructuredData = null,
            Retry = null
        });

        var loaded = service.LoadConfiguration();

        Assert.Null(loaded.RateLimit);
        Assert.Null(loaded.Tools);
        Assert.Null(loaded.Orchestrator);
    }

    [Fact]
    public void LoadConfiguration_UsesProviderEnvironmentApiKeyWhenUnset()
    {
        const string variable = "OPENAI_API_KEY";
        var original = Environment.GetEnvironmentVariable(variable);
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable(variable, "test-key");
            var service = new ConfigurationService(new ApplicationDataPathResolver(root));

            Assert.Equal("test-key", service.LoadConfiguration().LlmProvider.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }
}
