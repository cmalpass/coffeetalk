namespace CoffeeTalk.Models;

public class LlmProviderConfig
{
    public string Type { get; set; } = "openai"; // "openai" or "ollama"
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
}
