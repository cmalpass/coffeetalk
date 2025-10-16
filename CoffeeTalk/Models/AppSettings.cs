namespace CoffeeTalk.Models;

public class AppSettings
{
    public LlmProviderConfig LlmProvider { get; set; } = new();
    public List<PersonaConfig> Personas { get; set; } = new();
    public int MaxConversationTurns { get; set; } = 10;
    public bool ShowThinking { get; set; } = true;
}
