using Microsoft.SemanticKernel;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public static class KernelBuilderService
{
    public static Kernel BuildKernel(LlmProviderConfig config)
    {
        var builder = Kernel.CreateBuilder();

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

            default:
                throw new ArgumentException($"Unsupported LLM provider type: {config.Type}");
        }

        return builder.Build();
    }
}
