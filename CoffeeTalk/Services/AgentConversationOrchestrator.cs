using Microsoft.Agents.AI;
using CoffeeTalk.Models;
using Spectre.Console;

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

    public AgentConversationOrchestrator(
        List<AgentPersona> personas,
        CollaborativeMarkdownDocument doc,
        AppSettings settings,
        AgentOrchestrator? orchestrator = null,
        AgentEditor? editor = null,
        AgentDataExtractor? dataExtractor = null,
        AgentFactChecker? factChecker = null)
    {
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
            AnsiConsole.MarkupLine("[red]No personas configured. Please add personas to appsettings.json[/]");
            return;
        }

        AnsiConsole.MarkupLine($"\n[bold]🎯 Topic:[/] [cyan]{Markup.Escape(topic)}[/]\n");
        AnsiConsole.MarkupLine($"[bold]Participants:[/] {string.Join(", ", _personas.Select(a => Markup.Escape(a.Name)))}\n");
        
        if (_useOrchestrator)
        {
            AnsiConsole.MarkupLine("[bold]Mode:[/] [magenta]🎭 Orchestrated (AI-directed conversation flow)[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine("[bold]Mode:[/] [blue]🔄 Round-robin (sequential turns)[/]\n");
        }
        
        if (_interactiveMode)
        {
            AnsiConsole.MarkupLine("[bold]Interactive Mode:[/] [green]Enabled (Director's Chair)[/]");
            AnsiConsole.MarkupLine("[dim]You will be prompted to intervene after each turn.[/]\n");
        }

        AnsiConsole.MarkupLine("[bold]Starting conversation...[/]\n");
        AnsiConsole.Write(new Rule());

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

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Orchestrating...", async ctx =>
                    {
                        // Ask orchestrator who should speak next
                        var turnsRemaining = maxTotalTurns - totalTurns;
                        ctx.Status($"Orchestrator selecting next speaker (Turns remaining: {turnsRemaining})...");

                        selectedPersona = await _orchestrator!.SelectNextSpeakerAsync(currentMessage, conversationHistory, turnsRemaining);

                        if (selectedPersona != null)
                        {
                            ctx.Status($"{Markup.Escape(selectedPersona.Name)} is thinking...");
                            response = await selectedPersona.RespondAsync(currentMessage, conversationHistory);
                        }
                    });

                if (selectedPersona == null)
                {
                    AnsiConsole.MarkupLine("\n[yellow]⚠️  Orchestrator couldn't select a speaker. Ending conversation.[/]");
                    break;
                }

                DisplayResponse(selectedPersona.Name, response);
                
                conversationHistory.Add($"{selectedPersona.Name}: {response}");
                currentMessage = response;
                totalTurns++;

                // Show current document state after each turn
                var docPreview = selectedPersona.GetDocumentPreview();
                if (!string.IsNullOrWhiteSpace(docPreview))
                {
                    DisplayDocumentPreview(docPreview);
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
                    var (action, message) = await GetUserInterventionAsync();
                    if (action == "quit") break;
                    if (action == "inject" && !string.IsNullOrWhiteSpace(message))
                    {
                        AnsiConsole.MarkupLine($"\n[bold green]👤 Director:[/]: {Markup.Escape(message)}");
                        conversationHistory.Add($"Director (User): {message}");
                        currentMessage = $"Director (User): {message}";
                    }
                }

                // Orchestrator decides completion (already handled in SelectNextSpeakerAsync returning null)
            }
            catch (OperationCanceledException ex)
            {
                AnsiConsole.MarkupLine($"[red]❌ Operation canceled: {Markup.Escape(ex.Message)}[/]");
            }
            catch (TimeoutException ex)
            {
                AnsiConsole.MarkupLine($"[red]❌ Timeout: {Markup.Escape(ex.Message)}[/]");
            }
            catch (Exception ex) when (
                ex is not StackOverflowException &&
                ex is not OutOfMemoryException &&
                ex is not ThreadAbortException
            )
            {
                AnsiConsole.WriteException(ex);
            }

            AnsiConsole.Write(new Rule());
        }

        AnsiConsole.Write(new Rule("Conversation Ended"));
        AnsiConsole.MarkupLine($"\n[yellow]⏱️  Maximum turns ({maxTotalTurns}) reached. Conversation ended.[/]");

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
                        await AnsiConsole.Status()
                            .Spinner(Spinner.Known.Dots)
                            .StartAsync($"{Markup.Escape(persona.Name)} is thinking...", async ctx =>
                            {
                                response = await persona.RespondAsync(currentMessage, conversationHistory);
                            });
                    }
                    else
                    {
                        response = await persona.RespondAsync(currentMessage, conversationHistory);
                    }

                    DisplayResponse(persona.Name, response);
                    
                    conversationHistory.Add($"{persona.Name}: {response}");
                    currentMessage = response;
                    totalTurns++;

                    // Show current document state after each turn
                    var docPreview = persona.GetDocumentPreview();
                    if (!string.IsNullOrWhiteSpace(docPreview))
                    {
                        DisplayDocumentPreview(docPreview);
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
                        var (action, message) = await GetUserInterventionAsync();
                        if (action == "quit")
                        {
                            AnsiConsole.MarkupLine($"\n[yellow]Conversation manually ended by user.[/]");
                            await TryAutoSaveAsync();
                            return;
                        }
                        if (action == "inject" && !string.IsNullOrWhiteSpace(message))
                        {
                            AnsiConsole.MarkupLine($"\n[bold green]👤 Director:[/]: {Markup.Escape(message)}");
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
                        AnsiConsole.Write(new Rule("Conversation Complete"));
                        AnsiConsole.MarkupLine("\n[bold green]✅ Conversation goal appears to be reached![/]");
                        AnsiConsole.MarkupLine($"Total turns: {turn + 1} (across {_personas.Count} personas)");

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
                    AnsiConsole.MarkupLine($"[red]❌ Operation canceled: {Markup.Escape(ex.Message)}[/]");
                }
                catch (TimeoutException ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Timeout: {Markup.Escape(ex.Message)}[/]");
                }
                catch (Exception ex) when (
                    ex is not StackOverflowException &&
                    ex is not OutOfMemoryException &&
                    ex is not ThreadAbortException
                )
                {
                    AnsiConsole.WriteException(ex);
                }
            }

            AnsiConsole.Write(new Rule());
        }

        AnsiConsole.Write(new Rule("Max Turns Reached"));
        AnsiConsole.MarkupLine($"\n[yellow]⏱️  Maximum turns ({_maxTurns}) reached. Conversation ended.[/]");

        if (_dataExtractor != null)
        {
            await _dataExtractor.ExtractAndSaveAsync(conversationHistory);
        }

        await TryAutoSaveAsync();
    }

    private void DisplayResponse(string name, string response)
    {
        var panel = new Panel(new Text(response))
            .Header($"[bold]{Markup.Escape(name)}[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);
    }

    private void DisplayDocumentPreview(string content)
    {
        var panel = new Panel(new Text(content))
            .Header("[bold cyan]Document State[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);

        AnsiConsole.Write(panel);
    }

    private async Task RunEditorIntervention(List<string> conversationHistory)
    {
        AnsiConsole.Write(new Rule("Editor Review") { Style = Style.Parse("magenta") });
        AnsiConsole.MarkupLine("\n[magenta]✂️  Refining document for clarity and conciseness...[/]");

        try
        {
            // Build context from recent conversation
            var recentContext = conversationHistory.TakeLast(6).ToList();
            var contextSummary = recentContext.Count > 0
                ? string.Join("\n", recentContext)
                : "No recent conversation";

            string editorResponse = string.Empty;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Editor is reviewing...", async ctx =>
                {
                    editorResponse = await _editor!.ReviewAndEditAsync(contextSummary);
                });
            
            var panel = new Panel(new Text(editorResponse))
                .Header("[bold magenta]Editor[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Magenta1);
            AnsiConsole.Write(panel);

            // Show updated document state
            if (_personas.Count > 0)
            {
                var docPreview = _personas[0].GetDocumentPreview();
                if (!string.IsNullOrWhiteSpace(docPreview))
                {
                    DisplayDocumentPreview(docPreview);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Editor review skipped (invalid operation): {Markup.Escape(ex.Message)}[/]");
        }
        catch (TimeoutException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Editor review skipped (timeout): {Markup.Escape(ex.Message)}[/]");
        }

        AnsiConsole.WriteLine();
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

    private Task TryAutoSaveAsync()
    {
        try
        {
            var path = _doc.SaveToFile("conversation.md");

            AnsiConsole.MarkupLine($"[green]✓ Auto-saved collaborative document ({Markup.Escape(path)})[/]");
        }
        catch (System.IO.IOException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Auto-save failed (IO error): {Markup.Escape(ex.Message)}[/]");
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Auto-save failed (access denied): {Markup.Escape(ex.Message)}[/]");
        }
        
        return Task.CompletedTask;
    }

    private async Task SummarizeHistoryAsync(List<string> conversationHistory)
    {
        if (_personas.Count == 0) return;

        var historyToSummarize = conversationHistory.Take(10).ToList();
        var remainingHistory = conversationHistory.Skip(10).ToList();
        var historyText = string.Join("\n", historyToSummarize);

        try
        {
            await AnsiConsole.Status().StartAsync("Summarizing context...", async ctx =>
            {
                string summary = string.Empty;

                if (_orchestrator != null)
                {
                    summary = await _orchestrator.SummarizeAsync(historyText);
                }
                else
                {
                    // Fallback: Use the first available persona to summarize
                    // Note: We need a way to run a direct prompt without the conversational wrapper.
                    // Since AgentPersona.RespondAsync is opinionated, we might need a public method to run a raw prompt.
                    // For now, we will use a simpler fallback to avoid breaking encapsulation,
                    // or ideally, we should expose a 'Summarize' capability on AgentPersona.
                    // Given the constraints, we'll try to use the first persona if possible,
                    // but since we can't easily access the raw agent, we will assume
                    // a truncation is the safest fallback without refactoring AgentPersona.

                    // Actually, let's try to be smarter. We can't access ._agent.
                    // But we can just truncate.
                    conversationHistory.RemoveRange(0, 5);
                    conversationHistory.Insert(0, "[... older history removed to save context ...]");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(summary))
                {
                    conversationHistory.Clear();
                    conversationHistory.Add($"[Summary of previous turns]: {summary}");
                    conversationHistory.AddRange(remainingHistory);
                    AnsiConsole.MarkupLine("[dim]History summarized to save tokens.[/]");
                }
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[dim red]Summarization failed: {ex.Message}[/]");
        }
    }

    private Task<(string Action, string Message)> GetUserInterventionAsync()
    {
        AnsiConsole.WriteLine();
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Director's Chair[/]: What would you like to do?")
                .AddChoices(new[] {
                    "Continue",
                    "Inject Direction/Feedback",
                    "End Conversation"
                }));

        if (selection == "End Conversation")
        {
            return Task.FromResult(("quit", string.Empty));
        }

        if (selection == "Inject Direction/Feedback")
        {
            var message = AnsiConsole.Ask<string>("[green]Enter your instruction:[/]");
            return Task.FromResult(("inject", message));
        }

        return Task.FromResult(("continue", string.Empty));
    }

}
