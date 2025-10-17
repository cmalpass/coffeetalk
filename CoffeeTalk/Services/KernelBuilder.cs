using Microsoft.SemanticKernel;
using CoffeeTalk.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeTalk.Services;

public static class KernelBuilderService
{
    public static Kernel BuildKernel(LlmProviderConfig config)
    {
        var builder = Kernel.CreateBuilder();

        // Shared collaborative document as a singleton so all agents share the same state
        builder.Services.AddSingleton<CollaborativeMarkdownDocument>();
    builder.Services.AddTransient<ToolingVerifier>();

        switch (config.Type.ToLower())
        {
            case "openai":
                builder.AddOpenAIChatCompletion(
                    modelId: config.ModelId,
                    apiKey: config.ApiKey);
                break;

            case "ollama":
                // Ollama uses OpenAI-compatible API
                builder.AddOpenAIChatCompletion(
                    modelId: config.ModelId,
                    endpoint: new Uri(config.Endpoint),
                    apiKey: "not-needed"); // Ollama doesn't require API key
                break;

            case "azureopenai":
                // Azure OpenAI expects a deployment name (not model name), endpoint and api key
                var deployment = string.IsNullOrWhiteSpace(config.DeploymentName) ? config.ModelId : config.DeploymentName;
                if (string.IsNullOrWhiteSpace(deployment))
                {
                    throw new ArgumentException("Azure OpenAI requires a DeploymentName (or ModelId used as DeploymentName)");
                }
                if (string.IsNullOrWhiteSpace(config.Endpoint))
                {
                    throw new ArgumentException("Azure OpenAI requires an Endpoint (e.g., https://<resource>.openai.azure.com)");
                }
                if (string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    throw new ArgumentException("Azure OpenAI requires an ApiKey");
                }

                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: deployment,
                    endpoint: config.Endpoint,
                    apiKey: config.ApiKey);
                break;

            default:
                throw new ArgumentException($"Unsupported LLM provider type: {config.Type}");
        }

        // Build the kernel
        var kernel = builder.Build();

        // Register markdown collaboration tools as a plugin, backed by the shared document instance
        var sharedDoc = kernel.Services.GetRequiredService<CollaborativeMarkdownDocument>();
        var tools = new MarkdownTools(sharedDoc);
        kernel.Plugins.AddFromObject(tools, pluginName: "markdown");

        return kernel;
    }
}
