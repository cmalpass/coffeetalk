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
            Tools = new ToolsConfig { EnableFallbackJsonTools = false, RequireToolsVerification = false }
        };

        await service.SaveSettingsAsync(settings);
        var loaded = service.LoadConfiguration();

        Assert.True(Path.IsPathFullyQualified(resolver.ConfigurationFilePath));
        Assert.Equal(false, loaded.Tools?.EnableFallbackJsonTools);
        Assert.Equal(false, loaded.Tools?.RequireToolsVerification);
    }
}
