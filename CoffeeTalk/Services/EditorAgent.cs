using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using CoffeeTalk.Models;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CoffeeTalk.Services;

public class EditorAgent
{
    private readonly Kernel _kernel;
    private readonly EditorConfig _config;
    private readonly ChatHistory _chatHistory;
    private readonly CollaborativeMarkdownDocument _doc;
    private readonly RateLimiter? _rateLimiter;

    public EditorAgent(Kernel kernel, EditorConfig config, RateLimiter? rateLimiter)
    {
        _kernel = kernel;
        _config = config;
        _rateLimiter = rateLimiter;
        _doc = _kernel.Services.GetService(typeof(CollaborativeMarkdownDocument)) as CollaborativeMarkdownDocument 
            ?? throw new InvalidOperationException("CollaborativeMarkdownDocument not found in kernel services");
        _chatHistory = new ChatHistory();
        _chatHistory.AddSystemMessage(_config.SystemPrompt);
        _chatHistory.AddSystemMessage(GetEditorGuidelines());
    }

    public async Task<string> ReviewAndEditAsync(string conversationContext)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

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

Prefer replacing existing sections over appending new content. Use markdown.ReplaceSection to rewrite sections to be concise and aligned with the template. Use markdown.InsertAfterHeading only when adding essential, brief content.";

        _chatHistory.AddUserMessage(prompt);

        // Throttle if rate limiter is configured
        if (_rateLimiter != null)
        {
            var estimatedTokens = _rateLimiter.EstimateTokens(prompt);
            await _rateLimiter.ThrottleAsync(estimatedTokens);
        }

        // Execute with retry logic
        var response = await RetryHandler.ExecuteWithRetryAsync(
            async () => await chatService.GetChatMessageContentAsync(_chatHistory, executionSettings, _kernel),
            "Editor review");
        var responseText = response.Content ?? "No edits suggested.";

        _chatHistory.AddAssistantMessage(responseText);

        // Account response tokens
        _rateLimiter?.AccountAdditionalTokens(_rateLimiter.EstimateTokens(responseText));

        return responseText;
    }

    private string GetEditorGuidelines()
    {
    return @"Available markdown tools for editing:
- markdown.SetTitle: Change the document title
- markdown.AddHeading: Add a new heading (use sparingly - prefer consolidating)
- markdown.AppendParagraph: Add content (only if truly needed)
- markdown.InsertAfterHeading: Add/replace content under a heading
- markdown.ReplaceSection: Replace the entire content of a section under a heading
- markdown.ListHeadings: See current structure

Editing priorities:
1. Remove verbose, rambling text
2. Consolidate redundant sections
3. Shorten paragraphs (max 2-4 sentences)
4. Eliminate duplicate or similar headings
5. Ensure each section is purposeful and aligns to the consensus template (Position, Key Reasons, Trade-offs, Final Recommendation)
6. Keep the document actionable and focused
7. Prefer ReplaceSection to refine and streamline content";
    }

    public void Reset()
    {
        // Keep system messages, clear conversation history
        var systemMessages = _chatHistory.Where(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System).ToList();
        _chatHistory.Clear();
        foreach (var msg in systemMessages)
        {
            _chatHistory.Add(msg);
        }
    }
}
