using Microsoft.Agents.AI;
using CoffeeTalk.Models;
using CoffeeTalk.Core.Interfaces;
using System.Text;

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
    private readonly bool _interactiveMode;
    private readonly bool _contextSummarization;
    private readonly AppSettings _settings;
    private readonly AgentDataExtractor? _dataExtractor;
    private readonly AgentFactChecker? _factChecker;
    private readonly IUserInterface _ui;

    public AgentConversationOrchestrator(
        IUserInterface ui,
        List<AgentPersona> personas,
        CollaborativeMarkdownDocument doc,
        AppSettings settings,
        AgentOrchestrator? orchestrator = null,
        AgentEditor? editor = null,
        AgentDataExtractor? dataExtractor = null,
        AgentFactChecker? factChecker = null)
    {
        _ui = ui;
        _personas = personas;
        _doc = doc;
        _settings = settings;
        _maxTurns = settings.MaxConversationTurns;
        _showThinking = settings.ShowThinking;
        _rateLimiter = new RateLimiter(settings.RateLimit);
        _rateLimiter.ResetConversation();
        _useOrchestrator = orchestrator != null;
        _orchestrator = orchestrator;
        _editor = editor;
        _editorInterventionFrequency = settings.Editor?.InterventionFrequency ?? 3;
        _interactiveMode = settings.InteractiveMode;
        _contextSummarization = settings.ContextSummarization;
        _dataExtractor = dataExtractor;
        _factChecker = factChecker;
    }

    public async Task StartConversationAsync(string topic)
    {
        if (_personas.Count == 0)
        {
            await _ui.ShowErrorAsync("[red]No personas configured. Please add personas to appsettings.json[/]");
            return;
        }

        var mode = _useOrchestrator
            ? "Orchestrated (AI-directed conversation flow)"
            : "Round-robin (sequential turns)";
        
        await _ui.ShowConversationHeaderAsync(topic, _personas.Select(a => a.Name), mode, _interactiveMode);

        await _ui.ShowMessageAsync("[bold]Starting conversation...[/]\n");
        await _ui.ShowRuleAsync();

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
                AgentPersona? selectedPersona = null;
                string response = string.Empty;

                await _ui.RunWithStatusAsync("Orchestrating...", async () =>
                {
                    // Ask orchestrator who should speak next
                    var turnsRemaining = maxTotalTurns - totalTurns;
                    await _ui.SetStatusAsync($"Orchestrator selecting next speaker (Turns remaining: {turnsRemaining})...");

                    selectedPersona = await _orchestrator!.SelectNextSpeakerAsync(currentMessage, conversationHistory, turnsRemaining);

                    if (selectedPersona != null)
                    {
                        await _ui.SetStatusAsync($"{Escape(selectedPersona.Name)} is thinking...");
                        response = await selectedPersona.RespondAsync(currentMessage, conversationHistory);
                    }
                });

                if (selectedPersona == null)
                {
                    await _ui.ShowMessageAsync("\n[yellow]⚠️  Orchestrator couldn't select a speaker. Ending conversation.[/]");
                    break;
                }

                await _ui.ShowAgentResponseAsync(selectedPersona.Name, response);
                
                conversationHistory.Add($"{selectedPersona.Name}: {response}");
                currentMessage = response;
                totalTurns++;

                // Show current document state after each turn
                var docPreview = selectedPersona.GetDocumentPreview();
                if (!string.IsNullOrWhiteSpace(docPreview))
                {
                    await _ui.ShowDocumentPreviewAsync(docPreview);
                }

                // Editor intervention - review and refine document periodically
                if (_editor != null && totalTurns % _editorInterventionFrequency == 0)
                {
                    await RunEditorIntervention(conversationHistory);
                }

                // Fact Checker
                if (_factChecker != null)
                {
                    // Check the last message
                    await _factChecker.CheckAsync(response);
                }

                // Context Summarization
                if (_contextSummarization && conversationHistory.Count > 15)
                {
                    await SummarizeHistoryAsync(conversationHistory);
                }

                // Interactive Mode Check
                if (_interactiveMode)
                {
                    var (action, message) = await _ui.GetUserInterventionAsync();
                    if (action == "quit") break;
                    if (action == "inject" && !string.IsNullOrWhiteSpace(message))
                    {
                        await _ui.ShowMessageAsync($"\n[bold green]👤 Director:[/]: {Escape(message)}");
                        conversationHistory.Add($"Director (User): {message}");
                        currentMessage = $"Director (User): {message}";
                    }
                }

                // Orchestrator decides completion (already handled in SelectNextSpeakerAsync returning null)
            }
            catch (OperationCanceledException ex)
            {
                await _ui.ShowErrorAsync($"[red]❌ Operation canceled: {Escape(ex.Message)}[/]");
            }
            catch (TimeoutException ex)
            {
                await _ui.ShowErrorAsync($"[red]❌ Timeout: {Escape(ex.Message)}[/]");
            }
            catch (Exception ex) when (
                ex is not StackOverflowException &&
                ex is not OutOfMemoryException &&
                ex is not ThreadAbortException
            )
            {
                await _ui.ShowErrorAsync(ex.ToString());
            }

            await _ui.ShowRuleAsync();
        }

        await _ui.ShowRuleAsync("Conversation Ended");
        await _ui.ShowMessageAsync($"\n[yellow]⏱️  Maximum turns ({maxTotalTurns}) reached. Conversation ended.[/]");

        if (_dataExtractor != null)
        {
            await _dataExtractor.ExtractAndSaveAsync(conversationHistory);
        }

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
                    string response = string.Empty;

                    if (_showThinking)
                    {
                        await _ui.RunWithStatusAsync($"{Escape(persona.Name)} is thinking...", async () =>
                        {
                            response = await persona.RespondAsync(currentMessage, conversationHistory);
                        });
                    }
                    else
                    {
                        response = await persona.RespondAsync(currentMessage, conversationHistory);
                    }

                    await _ui.ShowAgentResponseAsync(persona.Name, response);
                    
                    conversationHistory.Add($"{persona.Name}: {response}");
                    currentMessage = response;
                    totalTurns++;

                    // Show current document state after each turn
                    var docPreview = persona.GetDocumentPreview();
                    if (!string.IsNullOrWhiteSpace(docPreview))
                    {
                        await _ui.ShowDocumentPreviewAsync(docPreview);
                    }

                    // Editor intervention - review and refine document periodically
                    if (_editor != null && totalTurns % _editorInterventionFrequency == 0)
                    {
                        await RunEditorIntervention(conversationHistory);
                    }

                    // Fact Checker
                    if (_factChecker != null)
                    {
                        await _factChecker.CheckAsync(response);
                    }

                    // Context Summarization
                    if (_contextSummarization && conversationHistory.Count > 15)
                    {
                        await SummarizeHistoryAsync(conversationHistory);
                    }

                    // Interactive Mode Check
                    if (_interactiveMode)
                    {
                        var (action, message) = await _ui.GetUserInterventionAsync();
                        if (action == "quit")
                        {
                            await _ui.ShowMessageAsync($"\n[yellow]Conversation manually ended by user.[/]");
                            await TryAutoSaveAsync();
                            return;
                        }
                        if (action == "inject" && !string.IsNullOrWhiteSpace(message))
                        {
                            await _ui.ShowMessageAsync($"\n[bold green]👤 Director:[/]: {Escape(message)}");
                            conversationHistory.Add($"Director (User): {message}");
                            currentMessage = $"Director (User): {message}";

                            // In round-robin, we might want the NEXT persona to respond to this,
                            // or maybe we just let the loop continue.
                            // Currently, the loop continues to the next persona in the list.
                        }
                    }

                    // Check if the conversation goal seems to be reached
                    if (IsConversationComplete(response, turn))
                    {
                        await _ui.ShowRuleAsync("Conversation Complete");
                        await _ui.ShowMessageAsync("\n[bold green]✅ Conversation goal appears to be reached![/]");
                        await _ui.ShowMessageAsync($"Total turns: {turn + 1} (across {_personas.Count} personas)");

                        if (_dataExtractor != null)
                        {
                            await _dataExtractor.ExtractAndSaveAsync(conversationHistory);
                        }

                        await TryAutoSaveAsync();
                        return;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    await _ui.ShowErrorAsync($"[red]❌ Operation canceled: {Escape(ex.Message)}[/]");
                }
                catch (TimeoutException ex)
                {
                    await _ui.ShowErrorAsync($"[red]❌ Timeout: {Escape(ex.Message)}[/]");
                }
                catch (Exception ex) when (
                    ex is not StackOverflowException &&
                    ex is not OutOfMemoryException &&
                    ex is not ThreadAbortException
                )
                {
                    await _ui.ShowErrorAsync(ex.ToString());
                }
            }

            await _ui.ShowRuleAsync();
        }

        await _ui.ShowRuleAsync("Max Turns Reached");
        await _ui.ShowMessageAsync($"\n[yellow]⏱️  Maximum turns ({_maxTurns}) reached. Conversation ended.[/]");

        if (_dataExtractor != null)
        {
            await _dataExtractor.ExtractAndSaveAsync(conversationHistory);
        }

        await TryAutoSaveAsync();
    }

    private async Task RunEditorIntervention(List<string> conversationHistory)
    {
        await _ui.ShowRuleAsync("Editor Review");
        await _ui.ShowMessageAsync("\n[magenta]✂️  Refining document for clarity and conciseness...[/]");

        try
        {
            // Build context from recent conversation
            var recentContext = conversationHistory.TakeLast(6).ToList();
            var contextSummary = recentContext.Count > 0
                ? string.Join("\n", recentContext)
                : "No recent conversation";

            string editorResponse = string.Empty;
            await _ui.RunWithStatusAsync("Editor is reviewing...", async () =>
            {
                editorResponse = await _editor!.ReviewAndEditAsync(contextSummary);
            });
            
            await _ui.ShowAgentResponseAsync("Editor", editorResponse);

            // Show updated document state
            if (_personas.Count > 0)
            {
                var docPreview = _personas[0].GetDocumentPreview();
                if (!string.IsNullOrWhiteSpace(docPreview))
                {
                    await _ui.ShowDocumentPreviewAsync(docPreview);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            await _ui.ShowMessageAsync($"[yellow]⚠️  Editor review skipped (invalid operation): {Escape(ex.Message)}[/]");
        }
        catch (TimeoutException ex)
        {
            await _ui.ShowMessageAsync($"[yellow]⚠️  Editor review skipped (timeout): {Escape(ex.Message)}[/]");
        }

        await _ui.ShowMessageAsync("");
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

            await _ui.ShowMessageAsync($"[green]✓ Auto-saved collaborative document ({Escape(path)})[/]");
        }
        catch (System.IO.IOException ex)
        {
            await _ui.ShowErrorAsync($"[yellow]⚠️  Auto-save failed (IO error): {Escape(ex.Message)}[/]");
        }
        catch (UnauthorizedAccessException ex)
        {
            await _ui.ShowErrorAsync($"[yellow]⚠️  Auto-save failed (access denied): {Escape(ex.Message)}[/]");
        }
    }

    private async Task SummarizeHistoryAsync(List<string> conversationHistory)
    {
        if (_personas.Count == 0) return;

        var historyToSummarize = conversationHistory.Take(10).ToList();
        var remainingHistory = conversationHistory.Skip(10).ToList();
        var historyText = string.Join("\n", historyToSummarize);

        try
        {
            await _ui.RunWithStatusAsync("Summarizing context...", async () =>
            {
                string summary = string.Empty;

                if (_orchestrator != null)
                {
                    summary = await _orchestrator.SummarizeAsync(historyText);
                }
                else
                {
                    // Fallback: Use the first available persona to summarize
                    conversationHistory.RemoveRange(0, 5);
                    conversationHistory.Insert(0, "[... older history removed to save context ...]");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(summary))
                {
                    conversationHistory.Clear();
                    conversationHistory.Add($"[Summary of previous turns]: {summary}");
                    conversationHistory.AddRange(remainingHistory);
                    await _ui.ShowMessageAsync("[dim]History summarized to save tokens.[/]");
                }
            });
        }
        catch (Exception ex)
        {
            await _ui.ShowErrorAsync($"[dim red]Summarization failed: {ex.Message}[/]");
        }
    }

    // Helper to escape markup since we are using Spectre Console conventions in strings still
    private string Escape(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
        // Note: Generic escaping might be needed if UI implementation relies on markup.
        // For now we assume the string passing uses Spectre-like markup or plain text.
        // If the UI is Blazor, we might need to handle this differently.
        // Ideally we should pass plain text and let the UI handle formatting, or use a rich text model.
        // For simplicity, we keep the markup strings and let the implementations handle them.
    }
}
