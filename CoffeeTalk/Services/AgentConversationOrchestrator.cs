using Microsoft.Agents.AI;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

/// <summary>
/// Orchestrates multi-persona conversations using Microsoft Agent Framework
/// </summary>
public class AgentConversationOrchestrator
{
    private readonly List<AgentPersona> _personas = new();
    private readonly CollaborativeMarkdownDocument _doc;
    private readonly int _maxTurns;
    private readonly bool _showThinking;
    private readonly RateLimiter? _rateLimiter;
    private readonly AgentOrchestrator? _orchestrator;
    private readonly bool _useOrchestrator;
    private readonly AgentEditor? _editor;
    private readonly int _editorInterventionFrequency;

    public AgentConversationOrchestrator(
        List<AgentPersona> personas,
        CollaborativeMarkdownDocument doc,
        AppSettings settings,
        AgentOrchestrator? orchestrator = null,
        AgentEditor? editor = null)
    {
        _personas = personas;
        _doc = doc;
        _maxTurns = settings.MaxConversationTurns;
        _showThinking = settings.ShowThinking;
        _rateLimiter = new RateLimiter(settings.RateLimit);
        _rateLimiter.ResetConversation();
        _useOrchestrator = orchestrator != null;
        _orchestrator = orchestrator;
        _editor = editor;
        _editorInterventionFrequency = settings.Editor?.InterventionFrequency ?? 3;
    }

    public async Task StartConversationAsync(string topic)
    {
        if (_personas.Count == 0)
        {
            Console.WriteLine("No personas configured. Please add personas to appsettings.json");
            return;
        }

        Console.WriteLine($"\n🎯 Topic: {topic}\n");
        Console.WriteLine($"Participants: {string.Join(", ", _personas.Select(a => a.Name))}\n");
        
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
        int maxTotalTurns = _maxTurns * _personas.Count; // Total individual turns allowed

        while (totalTurns < maxTotalTurns)
        {
            try
            {
                // Ask orchestrator who should speak next
                var turnsRemaining = maxTotalTurns - totalTurns;
                var selectedPersona = await _orchestrator!.SelectNextSpeakerAsync(currentMessage, conversationHistory, turnsRemaining);

                if (selectedPersona == null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n⚠️  Orchestrator couldn't select a speaker. Ending conversation.");
                    Console.ResetColor();
                    break;
                }

                Console.WriteLine($"\n💬 {selectedPersona.Name}:");
                
                if (_showThinking)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  [Thinking...]");
                    Console.ResetColor();
                }

                var response = await selectedPersona.RespondAsync(currentMessage, conversationHistory);
                Console.WriteLine($"  {response}");
                
                conversationHistory.Add($"{selectedPersona.Name}: {response}");
                currentMessage = response;
                totalTurns++;

                // Show current document state after each turn
                var docPreview = selectedPersona.GetDocumentPreview();
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
            foreach (var persona in _personas)
            {
                try
                {
                    Console.WriteLine($"\n💬 {persona.Name}:");
                    
                    if (_showThinking)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("  [Thinking...]");
                        Console.ResetColor();
                    }

                    var response = await persona.RespondAsync(currentMessage, conversationHistory);
                    Console.WriteLine($"  {response}");
                    
                    conversationHistory.Add($"{persona.Name}: {response}");
                    currentMessage = response;
                    totalTurns++;

                    // Show current document state after each turn
                    var docPreview = persona.GetDocumentPreview();
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
                        Console.WriteLine($"Total turns: {turn + 1} (across {_personas.Count} personas)");
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
            if (_personas.Count > 0)
            {
                var docPreview = _personas[0].GetDocumentPreview();
                if (!string.IsNullOrWhiteSpace(docPreview))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n  📄 Revised document state:\n{docPreview}");
                    Console.ResetColor();
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠️  Editor review skipped (invalid operation): {ex.Message}");
            Console.ResetColor();
        }
        catch (TimeoutException ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠️  Editor review skipped (timeout): {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine($"{new string('=', 80)}\n");
    }

    private bool IsConversationComplete(string response, int turn)
    {
        // For round-robin mode only: very conservative early completion
        // Require at least 80% of max turns before allowing early conclusion
        var maxTotalTurns = _maxTurns * _personas.Count;
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
            var path = _doc.SaveToFile("conversation.md");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Auto-saved collaborative document ({path})");
            Console.ResetColor();
        }
        catch (System.IO.IOException ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Auto-save failed (IO error): {ex.Message}");
            Console.ResetColor();
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Auto-save failed (access denied): {ex.Message}");
            Console.ResetColor();
        }
        
    }
}
