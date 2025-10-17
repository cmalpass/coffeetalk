namespace CoffeeTalk.Models;

public class AppSettings
{
    public LlmProviderConfig LlmProvider { get; set; } = new();
    public List<PersonaConfig> Personas { get; set; } = new();
    public DynamicPersonasConfig? DynamicPersonas { get; set; } = new();
    public int MaxConversationTurns { get; set; } = 10;
    public bool ShowThinking { get; set; } = true;
    public RateLimitConfig? RateLimit { get; set; }
    public ToolsConfig? Tools { get; set; }
    public OrchestratorConfig? Orchestrator { get; set; }
    public RetryConfig? Retry { get; set; }
    public EditorConfig? Editor { get; set; }
}
