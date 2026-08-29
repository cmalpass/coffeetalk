namespace CoffeeTalk.Models;

public class OrchestratorConfig
{
    public bool Enabled { get; set; }
    public bool UseDynamicPersonaSelection { get; set; }
    public string? BaseSystemPrompt { get; set; }

    /// <summary>
    /// Maximum number of consensus verification attempts before the conversation is
    /// terminated with <see cref="ConversationTerminationReason.ConsensusBudgetExhausted"/>.
    /// This is intentionally decoupled from the turn budget so consensus re-deliberation
    /// cannot consume the entire conversation cost on LLM fan-out.
    /// </summary>
    public int? MaxConsensusAttempts { get; set; }

    /// <summary>
    /// Maximum number of per-persona consensus assessments that may run concurrently
    /// during a single consensus attempt. Bounds the N+1 LLM fan-out to a fixed,
    /// predictable concurrency.
    /// </summary>
    public int? MaxConsensusConcurrency { get; set; }

    public const int DefaultMaxConsensusAttempts = 2;
    public const int DefaultMaxConsensusConcurrency = 2;

    public const string DefaultBaseSystemPrompt = @"You are a conversation orchestrator managing a collaborative discussion between multiple personas.

Your role:
- Analyze the current conversation state and document progress
- Select which persona should speak next based on their expertise and the conversation needs
- Ensure balanced participation while prioritizing the most relevant voice at each stage
- Guide the conversation toward a complete, well-structured document
- Recognize when the conversation goal has been achieved

When selecting a persona, consider:
- Their unique expertise and perspective
- What the document currently needs (structure, content, refinement, conclusion)
- Who hasn't contributed recently (for balanced participation)
- The current stage of the discussion (brainstorming, organizing, detailing, concluding)";
}
