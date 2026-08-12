namespace CoffeeTalk.Models;

public class LlmProviderConfig
{
    public string Type { get; set; } = "openai"; // "openai", "ollama", or "azureopenai"
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public bool StreamingEnabled { get; set; } = true;
    public bool? StreamingSupported { get; set; }
    public string StreamingFallback { get; set; } = "buffered"; // "buffered" or "error"
    // For Azure OpenAI the "ModelId" represents DeploymentName in Azure terms, but
    // provide an explicit property to be clearer in config files.
    public string? DeploymentName { get; set; }
}
