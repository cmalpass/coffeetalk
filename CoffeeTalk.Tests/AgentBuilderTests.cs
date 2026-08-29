using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class AgentBuilderTests
{
    private static LlmProviderConfig AzureConfig(string? apiKey = null, bool withEndpoint = true) =>
        new()
        {
            Type = "azureopenai",
            ApiKey = apiKey ?? string.Empty,
            Endpoint = withEndpoint ? "https://example.openai.azure.com/" : string.Empty,
            DeploymentName = "gpt-deployment",
            ModelId = "gpt-4o-mini"
        };

    [Theory]
    [InlineData("real-key", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void UsesApiKeyAuth_ReflectsApiKeyPresence(string? apiKey, bool expected)
    {
        var config = AzureConfig(apiKey);
        Assert.Equal(expected, AgentBuilder.UsesApiKeyAuth(config));
    }

    [Fact]
    public void UsesApiKeyAuth_TrimsWhitespaceApiKey()
    {
        var config = AzureConfig("  ");
        Assert.False(AgentBuilder.UsesApiKeyAuth(config));
    }

    [Fact]
    public void CreateAgent_WithApiKey_PicksApiKeyAuthPath()
    {
        var config = AzureConfig("secret-key");

        var agent = AgentBuilder.CreateAgent(config, "tester", "You are a test agent.");

        Assert.NotNull(agent);
    }

    [Fact]
    public void CreateAgent_WithoutApiKey_DoesNotThrowAndPicksEntraPath()
    {
        var config = AzureConfig(apiKey: null, withEndpoint: true);

        var agent = AgentBuilder.CreateAgent(config, "tester", "You are a test agent.");

        Assert.NotNull(agent);
    }

    [Fact]
    public void CreateAgent_AzureMissingEndpoint_Throws()
    {
        var config = AzureConfig(apiKey: "key", withEndpoint: false);

        var ex = Assert.Throws<ArgumentException>(() =>
            AgentBuilder.CreateAgent(config, "tester", "instructions"));

        Assert.Contains("Endpoint", ex.Message);
    }

    [Fact]
    public void CreateAgent_AzureMissingDeployment_Throws()
    {
        var config = AzureConfig(apiKey: "key");
        config.DeploymentName = null;
        config.ModelId = string.Empty;

        var ex = Assert.Throws<ArgumentException>(() =>
            AgentBuilder.CreateAgent(config, "tester", "instructions"));

        Assert.Contains("DeploymentName", ex.Message);
    }

    [Fact]
    public void CreateAgent_UnsupportedType_Throws()
    {
        var config = AzureConfig("key");
        config.Type = "unknown-provider";

        var ex = Assert.Throws<ArgumentException>(() =>
            AgentBuilder.CreateAgent(config, "tester", "instructions"));

        Assert.Contains("Unsupported LLM provider type", ex.Message);
    }
}
