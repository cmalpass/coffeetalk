using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;

namespace CoffeeTalk.Services;

public class ToolingVerifier
{
    private readonly IChatCompletionService _chatService;
    private readonly CollaborativeMarkdownDocument _doc;

    public ToolingVerifier(IChatCompletionService chatService, CollaborativeMarkdownDocument doc)
    {
        _chatService = chatService;
        _doc = doc;
    }

    public async Task<bool> VerifyAsync(Kernel? kernel = null)
    {
        // Snapshot current state so we don't pollute the real doc
        var snapshot = _doc.Snapshot();
        try
        {
            var history = new ChatHistory();
            history.AddSystemMessage("Call the markdown tools directly. If you cannot, output the fallback tool JSON in a fenced block labeled 'tool' that would perform: SetTitle('Verification Title'), AddHeading('Verification Section',2), AppendParagraph('This is a verification paragraph.'). No extra commentary.");

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            };

            // Execute
            history.AddUserMessage("Please prepare a verification document and use the available tools to set a title and add a section.");
            var result = await RetryHandler.ExecuteWithRetryAsync(
                async () => await _chatService.GetChatMessageContentAsync(history, settings, kernel),
                "Tool verification");
            var text = result.Content ?? string.Empty;

            // If the tool didn't execute, try the fallback protocol inline
            if (!_doc.GetContent().Contains("Verification Title") && text.Contains("```tool"))
            {
                // crude inline executor similar to PersonaAgent fallback
                var payloadStart = text.IndexOf("```tool");
                var payloadEnd = text.IndexOf("```", payloadStart + 1);
                if (payloadStart >= 0 && payloadEnd > payloadStart)
                {
                    var payload = text.Substring(payloadStart + 7, payloadEnd - (payloadStart + 7)).Trim();
                    try
                    {
                        if (payload.StartsWith("["))
                        {
                            var arr = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<FallbackCall>>(payload);
                            if (arr != null)
                            {
                                foreach (var c in arr)
                                {
                                    ApplyFallback(c);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // Confirm doc changed as expected
            var content = _doc.GetContent();
            var ok = content.Contains("# Verification Title") && content.Contains("## Verification Section") && content.Contains("verification paragraph", StringComparison.OrdinalIgnoreCase);
            return ok;
        }
        catch
        {
            return false;
        }
        finally
        {
            // Restore original state
            _doc.Restore(snapshot);
        }
    }
    
    // Helper types and methods
    private class FallbackCall
    {
        public string? tool { get; set; }
        public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? args { get; set; }
    }

    private void ApplyFallback(FallbackCall c)
    {
        if (c == null || string.IsNullOrEmpty(c.tool)) return;
        var args = c.args ?? new();
        switch (c.tool.ToLower())
        {
            case "markdown.settitle":
                if (args.TryGetValue("title", out var t)) _doc.SetTitle(t.GetString() ?? "");
                break;
            case "markdown.addheading":
                {
                    var text = args.TryGetValue("text", out var te) ? te.GetString() ?? "" : "";
                    var level = args.TryGetValue("level", out var le) && le.TryGetInt32(out var lv) ? lv : 2;
                    _doc.AddHeading(text, level);
                }
                break;
            case "markdown.appendparagraph":
                if (args.TryGetValue("text", out var pe)) _doc.AppendParagraph(pe.GetString() ?? "");
                break;
        }
    }
}
