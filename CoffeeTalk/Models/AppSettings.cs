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
    public bool InteractiveMode { get; set; }

    // New Features
    public bool DevilsAdvocate { get; set; }
    public bool ContextSummarization { get; set; }
    public StructuredDataConfig? StructuredData { get; set; }
    public bool FactChecking { get; set; }
}

// PersistedAppSettings mirrors AppSettings but omits sensitive fields like ApiKey
public class PersistedAppSettings
{
    public PersistedLlmProviderConfig LlmProvider { get; set; } = new PersistedLlmProviderConfig();
    public List<PersonaConfig> Personas { get; set; } = new List<PersonaConfig>();
    public int MaxConversationTurns { get; set; }
    public bool ShowThinking { get; set; }
    public bool InteractiveMode { get; set; }

    // New Features
    public bool DevilsAdvocate { get; set; }
    public bool ContextSummarization { get; set; }
    public StructuredDataConfig? StructuredData { get; set; }
    public bool FactChecking { get; set; }

    public RateLimitConfig? RateLimit { get; set; }
    public RetryConfig? Retry { get; set; }
    public OrchestratorConfig? Orchestrator { get; set; }
    public EditorConfig? Editor { get; set; }
    public DynamicPersonasConfig? DynamicPersonas { get; set; }
}

public class PersistedLlmProviderConfig
{
    public string Type { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    // ApiKey is intentionally omitted
}
