using Microsoft.Agents.AI;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

/// <summary>
/// Represents a persona implemented using Microsoft Agent Framework
/// </summary>
public class AgentPersona
{
    private readonly AIAgent _agent;
    private readonly PersonaConfig _config;
    private readonly CollaborativeMarkdownDocument _doc;
    private readonly RateLimiter? _rateLimiter;
    private readonly int _maxTurns;
    private readonly int _agentCount;

    public string Name => _config.Name;
    public string SystemPrompt => _config.SystemPrompt;

    public AgentPersona(
        AIAgent agent,
        PersonaConfig config,
        CollaborativeMarkdownDocument doc,
        RateLimiter? rateLimiter,
        int maxTurns,
        int agentCount)
    {
        _agent = agent;
        _config = config;
        _doc = doc;
        _rateLimiter = rateLimiter;
        _maxTurns = maxTurns;
        _agentCount = agentCount;
    }

    public async Task<string> RespondAsync(string currentMessage, List<string> conversationHistory)
    {
        // Build context from recent conversation history (last 3 messages to reduce tokens)
        var recentHistory = conversationHistory.TakeLast(3).ToList();
        var contextMessage = recentHistory.Count > 0
            ? $"Recent conversation:\n{string.Join("\n", recentHistory)}\n\nCurrent message: {currentMessage}"
            : currentMessage;

        // Add current document state as context
        var docState = GetDocumentState();
        if (!string.IsNullOrWhiteSpace(docState))
        {
            contextMessage = $"Current document state:\n{docState}\n\n{contextMessage}";
        }

        // Calculate current turn number
        var currentTurn = (conversationHistory.Count / _agentCount) + 1;
        var turnsRemaining = _maxTurns - currentTurn;
        if (turnsRemaining <= 2)
        {
            contextMessage = $"⚠️ IMPORTANT: Only {turnsRemaining} turn(s) remaining. Focus on wrapping up and reaching a clear conclusion.\n\n{contextMessage}";
        }

        // Add collaboration guidelines as context
        contextMessage = $"{GetPersonaCollaborationGuidelines()}\n\n{contextMessage}";

        // Throttle based on an estimated token count
        var estimatedTokens = _rateLimiter?.EstimateTokens(contextMessage) ?? 0;
        if (_rateLimiter != null)
        {
            await _rateLimiter.ThrottleAsync(estimatedTokens);
        }

        // Execute with retry logic for rate limiting (HTTP 429)
        string responseText;
        try
        {
            var response = await RetryHandler.ExecuteWithRetryAsync(
                async () => await _agent.RunAsync(contextMessage),
                $"{Name} response");
            responseText = response.ToString();
        }
        catch (Exception ex)
        {
            responseText = $"Error: {ex.Message}";
        }

        // Account response tokens approximately
        _rateLimiter?.AccountAdditionalTokens(_rateLimiter.EstimateTokens(responseText));

        return responseText;
    }

    private string GetPersonaCollaborationGuidelines()
    {
        var totalTurnsForAllPersonas = _maxTurns * _agentCount;
        return $@"You are collaborating with {_agentCount} persona(s) to produce ONE cohesive, CONCISE consensus document.
You have a maximum of {_maxTurns} rounds (total of {totalTurnsForAllPersonas} individual turns across all personas) to complete the work.

DELIVERABLE: A short markdown document that captures the agreed-upon stance.
Template:
# <Concise Title>
## Position
<1 short paragraph stating the agreed stance in plain language>
## Key Reasons
- <bullet 1>
- <bullet 2>
- <bullet 3>
## Trade-offs
- <bullet 1>
- <bullet 2>
## Final Recommendation
<1 short paragraph with the action-oriented recommendation>

CRITICAL GUIDELINES - CONCISENESS & CONSENSUS:
- BE CONCISE: Every sentence must serve a clear purpose. No rambling or verbose prose.
- AVOID NARRATIVE STYLE: This is a professional consensus statement, not an essay.
- SHORT PARAGRAPHS: Max 2-4 sentences. Use bullet points for lists.
- NO REDUNDANCY: Read what others have written. Don't repeat points already made.
- EDIT, DON'T JUST ADD: Use ReplaceSection to refine and consolidate content.
- PURPOSEFUL HEADINGS: Use only the template headings unless absolutely necessary.
- CONVERGE: If disagreement exists, capture the trade-off succinctly, then converge on a stance.

Completion Strategy:
- An editor will periodically review and refine the document for conciseness and coherence.
- As you approach the final rounds, prioritize convergence and finalize the recommendation.
- Avoid calling SaveToFile—the system auto-saves when the conversation finishes.";
    }

    private string GetDocumentState()
    {
        try
        {
            var headings = _doc.ListHeadings();
            return string.IsNullOrWhiteSpace(headings) ? "Document is empty" : headings;
        }
        catch
        {
            return string.Empty;
        }
    }

    public string GetDocumentPreview()
    {
        try
        {
            var headings = _doc.ListHeadings();
            if (string.IsNullOrWhiteSpace(headings)) return "  [Document is empty]";
            return string.Join("\n", headings.Split('\n').Select(h => $"  {h}"));
        }
        catch
        {
            return string.Empty;
        }
    }
}
