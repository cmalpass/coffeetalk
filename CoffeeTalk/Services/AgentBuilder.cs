using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using Azure.AI.OpenAI;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

/// <summary>
/// Builds AIAgent instances for different LLM providers using Microsoft Agent Framework
/// </summary>
public static class AgentBuilder
{
    public static AIAgent CreateAgent(LlmProviderConfig config, string name, string instructions, AIFunction[]? tools = null)
    {
        OpenAI.Chat.ChatClient chatClient = config.Type.ToLower() switch
        {
            "openai" => new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(config.ApiKey))
                .GetChatClient(config.ModelId),
            
            "ollama" => new OpenAI.OpenAIClient(
                new System.ClientModel.ApiKeyCredential("not-needed"), // Ollama doesn't require API key
                new OpenAI.OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) })
                .GetChatClient(config.ModelId),
            
            "azureopenai" => CreateAzureOpenAIClient(config),
            
            _ => throw new ArgumentException($"Unsupported LLM provider type: {config.Type}")
        };

        return chatClient.CreateAIAgent(
            name: name,
            instructions: instructions,
            tools: tools);
    }

    private static OpenAI.Chat.ChatClient CreateAzureOpenAIClient(LlmProviderConfig config)
    {
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

        // Create Azure OpenAI client with API key
        var azureClient = new AzureOpenAIClient(
            new Uri(config.Endpoint),
            new System.ClientModel.ApiKeyCredential(config.ApiKey));

        return azureClient.GetChatClient(deployment);
    }
}
