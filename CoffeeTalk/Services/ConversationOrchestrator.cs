using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using CoffeeTalk.Models;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CoffeeTalk.Services;

public class ConversationOrchestrator
{
    private readonly List<PersonaAgent> _agents = new();
    private readonly int _maxTurns;
    private readonly bool _showThinking;
    private readonly RateLimiter? _rateLimiter;
    private readonly OrchestratorAgent? _orchestrator;
    private readonly bool _useOrchestrator;
    private readonly EditorAgent? _editor;
    private readonly int _editorInterventionFrequency;

    public ConversationOrchestrator(Kernel kernel, AppSettings settings)
    {
        _maxTurns = settings.MaxConversationTurns;
        _showThinking = settings.ShowThinking;
        _rateLimiter = new RateLimiter(settings.RateLimit);
        _rateLimiter.ResetConversation();

        // Create an agent for each persona
        foreach (var persona in settings.Personas)
        {
            _agents.Add(new PersonaAgent(kernel, persona, _rateLimiter, _maxTurns, settings.Personas.Count));
        }

        // Create orchestrator if enabled
        _useOrchestrator = settings.Orchestrator?.Enabled ?? false;
        if (_useOrchestrator)
        {
            var orchestratorConfig = settings.Orchestrator ?? new OrchestratorConfig();
            _orchestrator = new OrchestratorAgent(kernel, orchestratorConfig, _agents);
        }

        // Create editor if enabled
        if (settings.Editor?.Enabled ?? false)
        {
            _editor = new EditorAgent(kernel, settings.Editor, _rateLimiter);
            _editorInterventionFrequency = settings.Editor.InterventionFrequency;
        }
    }

    public async Task StartConversationAsync(string topic)
    {
        if (_agents.Count == 0)
        {
            Console.WriteLine("No personas configured. Please add personas to appsettings.json");
            return;
        }

        Console.WriteLine($"\n🎯 Topic: {topic}\n");
        Console.WriteLine($"Participants: {string.Join(", ", _agents.Select(a => a.Name))}\n");
        
        if (_useOrchestrator)
        {
            Console.WriteLine("Mode: 🎭 Orchestrated (AI-directed conversation flow)\n");
        }
        else
        {
            Console.WriteLine("Mode: 🔄 Round-robin (sequential turns)\n");
        }
        
        Console.WriteLine("Starting conversation...\n");
        Console.WriteLine(new string('=', 80));

        var conversationHistory = new List<string>();
        var currentMessage = $"Let's discuss: {topic}";
        
        if (_useOrchestrator)
        {
            await RunOrchestratedConversationAsync(topic, conversationHistory, currentMessage);
        }
        else
        {
            await RunRoundRobinConversationAsync(conversationHistory, currentMessage);
        }
    }

    private async Task RunOrchestratedConversationAsync(string topic, List<string> conversationHistory, string currentMessage)
    {
        int totalTurns = 0;
        int maxTotalTurns = _maxTurns * _agents.Count; // Total individual turns allowed

        while (totalTurns < maxTotalTurns)
        {
            try
            {
                // Ask orchestrator who should speak next
                var turnsRemaining = maxTotalTurns - totalTurns;
                var selectedAgent = await _orchestrator!.SelectNextSpeakerAsync(currentMessage, conversationHistory, turnsRemaining);

                if (selectedAgent == null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n⚠️  Orchestrator couldn't select a speaker. Ending conversation.");
                    Console.ResetColor();
                    break;
                }

                Console.WriteLine($"\n💬 {selectedAgent.Name}:");
                
                if (_showThinking)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  [Thinking...]");
                    Console.ResetColor();
                }

                var response = await selectedAgent.RespondAsync(currentMessage, conversationHistory);
                Console.WriteLine($"  {response}");
                
                conversationHistory.Add($"{selectedAgent.Name}: {response}");
                currentMessage = response;
                totalTurns++;

                // Show current document state after each turn
                var docPreview = selectedAgent.GetDocumentPreview();
                if (!string.IsNullOrWhiteSpace(docPreview))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n  📄 Document state:\n{docPreview}");
                    Console.ResetColor();
                }

                // Editor intervention - review and refine document periodically
                if (_editor != null && totalTurns % _editorInterventionFrequency == 0)
                {
                    await RunEditorIntervention(conversationHistory);
                }

                // Orchestrator decides completion (already handled in SelectNextSpeakerAsync returning null)
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine($"\n{new string('-', 80)}");
        }

        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine($"\n⏱️  Maximum turns ({maxTotalTurns}) reached. Conversation ended.");
        await TryAutoSaveAsync();
    }

    private async Task RunRoundRobinConversationAsync(List<string> conversationHistory, string currentMessage)
    {
        int totalTurns = 0;
        for (int turn = 0; turn < _maxTurns; turn++)
        {
            foreach (var agent in _agents)
            {
                try
                {
                    Console.WriteLine($"\n💬 {agent.Name}:");
                    
                    if (_showThinking)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("  [Thinking...]");
                        Console.ResetColor();
                    }

                    var response = await agent.RespondAsync(currentMessage, conversationHistory);
                    Console.WriteLine($"  {response}");
                    
                    conversationHistory.Add($"{agent.Name}: {response}");
                    currentMessage = response;
                    totalTurns++;

                    // Show current document state after each turn
                    var docPreview = agent.GetDocumentPreview();
                    if (!string.IsNullOrWhiteSpace(docPreview))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"\n  📄 Document state:\n{docPreview}");
                        Console.ResetColor();
                    }

                    // Editor intervention - review and refine document periodically
                    if (_editor != null && totalTurns % _editorInterventionFrequency == 0)
                    {
                        await RunEditorIntervention(conversationHistory);
                    }

                    // Check if the conversation goal seems to be reached
                    if (IsConversationComplete(response, turn))
                    {
                        Console.WriteLine($"\n{new string('=', 80)}");
                        Console.WriteLine("\n✅ Conversation goal appears to be reached!");
                        Console.WriteLine($"Total turns: {turn + 1} (across {_agents.Count} personas)");
                        await TryAutoSaveAsync();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ❌ Error: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine($"\n{new string('-', 80)}");
        }

        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine($"\n⏱️  Maximum turns ({_maxTurns}) reached. Conversation ended.");
        await TryAutoSaveAsync();
    }

    private async Task RunEditorIntervention(List<string> conversationHistory)
    {
        Console.WriteLine($"\n{new string('=', 80)}");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("\n✂️  EDITOR REVIEW - Refining document for clarity and conciseness...");
        Console.ResetColor();

        if (_showThinking)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  [Reviewing...]");
            Console.ResetColor();
        }

        try
        {
            // Build context from recent conversation
            var recentContext = conversationHistory.TakeLast(6).ToList();
            var contextSummary = recentContext.Count > 0
                ? string.Join("\n", recentContext)
                : "No recent conversation";

            var editorResponse = await _editor!.ReviewAndEditAsync(contextSummary);
            
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"  {editorResponse}");
            Console.ResetColor();

            // Show updated document state
            if (_agents.Count > 0)
            {
                var docPreview = _agents[0].GetDocumentPreview();
                if (!string.IsNullOrWhiteSpace(docPreview))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n  📄 Revised document state:\n{docPreview}");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠️  Editor review skipped: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine($"{new string('=', 80)}\n");
    }

    private bool IsConversationComplete(string response, int turn)
    {
        // For round-robin mode only: very conservative early completion
        // Require at least 80% of max turns before allowing early conclusion
        var maxTotalTurns = _maxTurns * _agents.Count;
        var minTurnsBeforeConclusion = Math.Max(6, (int)(maxTotalTurns * 0.8));
        
        if (turn < minTurnsBeforeConclusion) return false;

        // Only match extremely explicit conclusion statements
        var completionIndicators = new[]
        {
            "this conversation is now complete",
            "our work here is finished",
            "ready to end this discussion"
        };

        var lowerResponse = response.ToLower();
        return completionIndicators.Any(indicator => lowerResponse.Contains(indicator));
    }

    private async Task TryAutoSaveAsync()
    {
        try
        {
            if (_agents.Count == 0) return;
            var kernel = typeof(PersonaAgent).GetField("_kernel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_agents[0]) as Kernel;
            if (kernel == null) return;

            var doc = kernel.Services.GetService(typeof(CollaborativeMarkdownDocument)) as CollaborativeMarkdownDocument;
            if (doc == null) return;
            var path = doc.SaveToFile("conversation.md");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Auto-saved collaborative document ({path})");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Auto-save failed: {ex.Message}");
            Console.ResetColor();
        }
    }
}

public class PersonaAgent
{
    private readonly Kernel _kernel;
    private readonly PersonaConfig _persona;
    private readonly ChatHistory _chatHistory;
    private readonly RateLimiter? _rateLimiter;
    private readonly CollaborativeMarkdownDocument? _doc;
    private readonly int _maxTurns;
    private readonly int _agentCount;

    public string Name => _persona.Name;
    public string SystemPrompt => _persona.SystemPrompt;

    public PersonaAgent(Kernel kernel, PersonaConfig persona, RateLimiter? rateLimiter, int maxTurns, int agentCount)
    {
        _kernel = kernel;
        _persona = persona;
        _rateLimiter = rateLimiter;
        _maxTurns = maxTurns;
        _agentCount = agentCount;
        _doc = _kernel.Services.GetService(typeof(CollaborativeMarkdownDocument)) as CollaborativeMarkdownDocument;
        _chatHistory = new ChatHistory();
        _chatHistory.AddSystemMessage(persona.SystemPrompt);
        _chatHistory.AddSystemMessage(GetPersonaCollaborationGuidelines());
    }

    public async Task<string> RespondAsync(string currentMessage, List<string> conversationHistory)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        // Build context from recent conversation history (last 3 messages to reduce tokens)
        var recentHistory = conversationHistory.TakeLast(3).ToList();
        var contextMessage = recentHistory.Count > 0
            ? $"Recent conversation:\n{string.Join("\n", recentHistory)}\n\nCurrent message: {currentMessage}"
            : currentMessage;

        // Add current document state as context before the user message
        var docState = GetDocumentState();
        if (!string.IsNullOrWhiteSpace(docState))
        {
            _chatHistory.AddSystemMessage($"Current document state:\n{docState}");
        }

        // Calculate current turn number
        var currentTurn = (conversationHistory.Count / _agentCount) + 1;
        var turnsRemaining = _maxTurns - currentTurn;
        if (turnsRemaining <= 2)
        {
            _chatHistory.AddSystemMessage($"⚠️ IMPORTANT: Only {turnsRemaining} turn(s) remaining. Focus on wrapping up and reaching a clear conclusion.");
        }

        _chatHistory.AddUserMessage(contextMessage);

        // Trim chat history to keep token count manageable (keep system messages + last 6 exchanges)
        // Note: trimming disabled temporarily to avoid tool message sequence issues
        // TrimChatHistory();

        // Throttle based on an estimated token count for the incoming context
        var charsPerToken = _rateLimiter?.EstimateTokens("a", 1) == 1 ? _rateLimiter!.EstimateTokens("aaaa", 4) : 1; // dummy to touch method
        var estCharsPerToken = _rateLimiter != null ? _rateLimiter.GetType().GetProperty("_cfg", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null ? 4.0 : 4.0 : 4.0; // default
        var estimatedTokens = _rateLimiter?.EstimateTokens(contextMessage, estCharsPerToken) ?? 0;
        if (_rateLimiter != null)
        {
            await _rateLimiter.ThrottleAsync(estimatedTokens);
        }

        // Execute with retry logic for rate limiting (HTTP 429)
        var response = await RetryHandler.ExecuteWithRetryAsync(
            async () => await chatService.GetChatMessageContentAsync(_chatHistory, executionSettings, _kernel),
            $"{Name} response");
        var responseText = response.Content ?? "I have no response.";

        _chatHistory.AddAssistantMessage(responseText);

        // Fallback protocol: parse tool-call JSON blocks and execute if present
        TryExecuteFallbackToolCalls(responseText);

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

Document Operations:
- To modify the shared document, output JSON tool calls in a fenced block labeled 'tool'.
- Use markdown.SetTitle to set the title.
- Use markdown.ReplaceSection to refine sections to the template above.
- Use markdown.InsertAfterHeading only when adding essential, concise content.

Completion Strategy:
- An editor will periodically review and refine the document for conciseness and coherence.
- As you approach the final rounds, prioritize convergence and finalize the recommendation.
- Avoid calling SaveToFile—the system auto-saves when the conversation finishes.

Tool Usage Fallback:
- If your model supports native function calling, use it directly. Otherwise, output tool calls in a fenced block:
   ```tool
    [{{""tool"":""markdown.SetTitle"",""args"":{{""title"":""Concise Title""}}}},
     {{""tool"":""markdown.ReplaceSection"",""args"":{{""headingText"":""Position"",""content"":""<1 short paragraph>""}}}},
     {{""tool"":""markdown.ReplaceSection"",""args"":{{""headingText"":""Key Reasons"",""content"":""- reason 1\n- reason 2\n- reason 3""}}}}]
   ```";
    }

    private void TryExecuteFallbackToolCalls(string responseText)
    {
        if (_doc == null || string.IsNullOrWhiteSpace(responseText)) return;
        try
        {
            var matches = Regex.Matches(responseText, "```tool\\s*(.*?)```", RegexOptions.Singleline);
            if (matches.Count == 0) return;

            foreach (Match m in matches)
            {
                var payload = m.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(payload)) continue;

                // Accept single object or array
                if (payload.StartsWith("["))
                {
                    var arr = JsonSerializer.Deserialize<List<ToolCallSpec>>(payload);
                    if (arr != null) ExecuteToolCalls(arr);
                }
                else
                {
                    var one = JsonSerializer.Deserialize<ToolCallSpec>(payload);
                    if (one != null) ExecuteToolCalls(new List<ToolCallSpec> { one });
                }
            }
        }
        catch
        {
            // ignore parse errors
        }
    }

    private void ExecuteToolCalls(List<ToolCallSpec> calls)
    {
        foreach (var call in calls)
        {
            if (call.Tool?.StartsWith("markdown.", StringComparison.OrdinalIgnoreCase) != true) continue;
            var name = call.Tool.Substring("markdown.".Length);
            var args = call.Args ?? new Dictionary<string, JsonElement>();

            switch (name.ToLower())
            {
                case "settitle":
                    if (args.TryGetValue("title", out var titleEl)) _doc!.SetTitle(titleEl.GetString() ?? "");
                    break;
                case "addheading":
                    {
                        var text = args.TryGetValue("text", out var tEl) ? tEl.GetString() ?? "" : "";
                        var level = args.TryGetValue("level", out var lEl) && lEl.TryGetInt32(out var lv) ? lv : 2;
                        _doc!.AddHeading(text, level);
                    }
                    break;
                case "appendparagraph":
                    if (args.TryGetValue("text", out var pEl)) _doc!.AppendParagraph(pEl.GetString() ?? "");
                    break;
                case "insertafterheading":
                    {
                        var headingText = args.TryGetValue("headingText", out var hEl) ? hEl.GetString() ?? "" : "";
                        var content = args.TryGetValue("content", out var cEl) ? cEl.GetString() ?? "" : "";
                        _doc!.InsertAfterHeading(headingText, content);
                    }
                    break;
                case "replacesection":
                    {
                        var headingText = args.TryGetValue("headingText", out var hEl) ? hEl.GetString() ?? "" : "";
                        var content = args.TryGetValue("content", out var cEl) ? cEl.GetString() ?? "" : "";
                        _doc!.ReplaceSection(headingText, content);
                    }
                    break;
                case "listheadings":
                    _ = _doc!.ListHeadings();
                    break;
                case "savetofile":
                    {
                        var path = args.TryGetValue("path", out var pathEl) ? pathEl.GetString() : null;
                        _doc!.SaveToFile(path ?? "conversation.md");
                    }
                    break;
                default:
                    break;
            }
        }
    }

    private class ToolCallSpec
    {
        public string? Tool { get; set; }
        public Dictionary<string, JsonElement>? Args { get; set; }
    }

    private string GetDocumentState()
    {
        if (_doc == null) return string.Empty;
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
        if (_doc == null) return string.Empty;
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

    private void TrimChatHistory()
    {
        // Keep system messages and last 6 user/assistant exchanges (12 messages)
        var systemMessages = _chatHistory.Where(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System).ToList();
        var nonSystemMessages = _chatHistory.Where(m => m.Role != Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System).ToList();

        if (nonSystemMessages.Count > 12)
        {
            var keepMessages = nonSystemMessages.TakeLast(12).ToList();
            _chatHistory.Clear();
            foreach (var msg in systemMessages)
            {
                _chatHistory.Add(msg);
            }
            foreach (var msg in keepMessages)
            {
                _chatHistory.Add(msg);
            }
        }
    }
}
