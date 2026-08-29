using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

/// <summary>
/// Builds AIAgent instances for different LLM providers using Microsoft Agent Framework
/// </summary>
public static class AgentBuilder
{
    public static AIAgent CreateAgent(LlmProviderConfig config, string name, string instructions, AIFunction[]? tools = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(instructions))
            throw new ArgumentException("Agent instructions are required.", nameof(instructions));

        OpenAI.Chat.ChatClient chatClient = config.Type.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAIClient(config),
            
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

        // Prefer an explicit API key when provided; otherwise fall back to Entra ID /
        // managed identity via DefaultAzureCredential so Azure-hosted workloads (AKS,
        // App Service, function apps) can authenticate without a key.
        if (UsesApiKeyAuth(config))
        {
            var keyedClient = new AzureOpenAIClient(
                new Uri(config.Endpoint),
                new System.ClientModel.ApiKeyCredential(config.ApiKey));
            return keyedClient.GetChatClient(deployment);
        }

        return CreateAzureClientWithEntraId(config, deployment);
    }

    /// <summary>
    /// Builds an <see cref="AzureOpenAIClient"/> authenticated with <see cref="DefaultAzureCredential"/>
    /// when no API key is configured.
    /// </summary>
    private static OpenAI.Chat.ChatClient CreateAzureClientWithEntraId(LlmProviderConfig config, string deployment)
    {
        AzureOpenAIClient azureClient;
        try
        {
            azureClient = new AzureOpenAIClient(
                new Uri(config.Endpoint),
                new DefaultAzureCredential());
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                "Azure OpenAI authentication failed: no Entra ID credential could be obtained from " +
                "DefaultAzureCredential. Set ApiKey (or AZURE_OPENAI_API_KEY) to use an API key, or " +
                "configure an Entra ID credential (managed identity, workload identity, or az login).",
                ex);
        }

        return azureClient.GetChatClient(deployment);
    }

    /// <summary>
    /// Decides which auth mode the Azure OpenAI client should use. When an API key is present
    /// the client is built with ApiKeyCredential; otherwise Entra ID (DefaultAzureCredential)
    /// is used. Exposed for testability.
    /// </summary>
    internal static bool UsesApiKeyAuth(LlmProviderConfig config) =>
        !string.IsNullOrWhiteSpace(config.ApiKey);

    private static OpenAI.Chat.ChatClient CreateOpenAIClient(LlmProviderConfig config)
    {
        var apiKey = string.IsNullOrWhiteSpace(config.ApiKey) ? "not-needed" : config.ApiKey;
        var credential = new System.ClientModel.ApiKeyCredential(apiKey);

        if (string.IsNullOrWhiteSpace(config.Endpoint))
            return new OpenAI.OpenAIClient(credential).GetChatClient(config.ModelId);

        return new OpenAI.OpenAIClient(
            credential,
            new OpenAI.OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) })
            .GetChatClient(config.ModelId);
    }
}
