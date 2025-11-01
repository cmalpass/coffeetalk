using Microsoft.Agents.AI;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

/// <summary>
/// Editor agent that reviews and refines the document using Microsoft Agent Framework
/// </summary>
public class AgentEditor
{
    private readonly AIAgent _agent;
    private readonly EditorConfig _config;
    private readonly CollaborativeMarkdownDocument _doc;
    private readonly RateLimiter? _rateLimiter;

    public AgentEditor(AIAgent agent, EditorConfig config, CollaborativeMarkdownDocument doc, RateLimiter? rateLimiter)
    {
        _agent = agent;
        _config = config;
        _doc = doc;
        _rateLimiter = rateLimiter;
    }

    public static string BuildSystemPrompt(EditorConfig config)
    {
        var basePrompt = config.SystemPrompt;
        var guidelines = @"

Available markdown tools for editing:
- SetTitle: Change the document title
- AddHeading: Add a new heading (use sparingly - prefer consolidating)
- AppendParagraph: Add content (only if truly needed)
- InsertAfterHeading: Add/replace content under a heading
- ReplaceSection: Replace the entire content of a section under a heading
- ListHeadings: See current structure

Editing priorities:
1. Remove verbose, rambling text
2. Consolidate redundant sections
3. Shorten paragraphs (max 2-4 sentences)
4. Eliminate duplicate or similar headings
5. Ensure each section is purposeful and aligns to the consensus template (Position, Key Reasons, Trade-offs, Final Recommendation)
6. Keep the document actionable and focused
7. Prefer ReplaceSection to refine and streamline content";

        return basePrompt + guidelines;
    }

    public async Task<string> ReviewAndEditAsync(string conversationContext)
    {
        // Get current document content
        var currentContent = _doc.GetContent();
        
        if (string.IsNullOrWhiteSpace(currentContent))
        {
            return "Document is empty - nothing to edit yet.";
        }

        // Build editing prompt
        var prompt = $@"Current document:
```markdown
{currentContent}
```

Recent conversation context:
{conversationContext}

Review this document and edit it for clarity, conciseness, and structure. Remove redundancy, consolidate similar points, and ensure it directly serves the main goal.

Restructure to the consensus template when possible:
- Title (H1)
- Position (concise paragraph)
- Key Reasons (bullets)
- Trade-offs (bullets)
- Final Recommendation (concise paragraph)

Prefer replacing existing sections over appending new content. Use ReplaceSection to rewrite sections to be concise and aligned with the template. Use InsertAfterHeading only when adding essential, brief content.";

        // Throttle if rate limiter is configured
        if (_rateLimiter != null)
        {
            var estimatedTokens = _rateLimiter.EstimateTokens(prompt);
            await _rateLimiter.ThrottleAsync(estimatedTokens);
        }

        // Execute with retry logic
        var response = await RetryHandler.ExecuteWithRetryAsync(
            async () => await _agent.RunAsync(prompt),
            "Editor review");
        var responseText = response.ToString();

        // Account response tokens
        _rateLimiter?.AccountAdditionalTokens(_rateLimiter.EstimateTokens(responseText));

        return responseText;
    }
}
