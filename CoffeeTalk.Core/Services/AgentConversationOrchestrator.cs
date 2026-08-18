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
    private readonly AgentOrchestrator? _orchestrator;
    private readonly bool _useOrchestrator;
    private readonly AgentEditor? _editor;
    private readonly int _editorInterventionFrequency;
    private readonly bool _interactiveMode;
    private readonly bool _contextSummarization;
    private readonly AppSettings _settings;
    private readonly AgentDataExtractor? _dataExtractor;
    private readonly AgentFactChecker? _factChecker;
    private readonly AgentMemoryExtractor? _memoryExtractor;
    private readonly IMemoryStore? _memoryStore;
    private string _currentTopic = string.Empty;
    private readonly IUserInterface _ui;

    public AgentConversationOrchestrator(
        IUserInterface ui,
        List<AgentPersona> personas,
        CollaborativeMarkdownDocument doc,
        AppSettings settings,
        AgentOrchestrator? orchestrator = null,
        AgentEditor? editor = null,
        AgentDataExtractor? dataExtractor = null,
        AgentFactChecker? factChecker = null,
        AgentMemoryExtractor? memoryExtractor = null,
        IMemoryStore? memoryStore = null)
    {
        _ui = ui;
        _personas = personas;
        _doc = doc;
        _settings = settings;
        _maxTurns = settings.MaxConversationTurns;
        _showThinking = settings.ShowThinking;
        _useOrchestrator = orchestrator != null;
        _orchestrator = orchestrator;
        _editor = editor;
        _editorInterventionFrequency = settings.Editor?.InterventionFrequency ?? 3;
        _interactiveMode = settings.InteractiveMode;
        _contextSummarization = settings.ContextSummarization;
        _dataExtractor = dataExtractor;
        _factChecker = factChecker;
        _memoryExtractor = memoryExtractor;
        _memoryStore = memoryStore;
    }

    public async Task StartConversationAsync(string topic, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _currentTopic = topic;
        if (_personas.Count == 0)
        {
            _ui.TerminationReason = ConversationTerminationReason.NoPersonas;
            await _ui.ShowErrorAsync("[red]No personas configured. Please add personas to appsettings.json[/]");
            return;
        }

        var mode = _useOrchestrator
            ? "Orchestrated (AI-directed conversation flow)"
            : "Round-robin (sequential turns)";
        
        await _ui.ShowConversationHeaderAsync(topic, _personas.Select(a => a.Name).ToList(), mode, _interactiveMode);

        await _ui.ShowMessageAsync("[bold]Starting conversation...[/]\n");
        await _ui.ShowRuleAsync();

        var conversationHistory = new List<string>();
        var currentMessage = $"Let's discuss: {topic}";
        if (_memoryStore is not null)
        {
            try
            {
                var query = topic.Length > (_settings.Memory?.MaxQueryLength ?? topic.Length)
                    ? topic[.._settings.Memory!.MaxQueryLength]
                    : topic;
                var recalled = await _memoryStore.SearchAsync(
                    query,
                    new MemorySearchOptions { Limit = _settings.Memory?.RecallLimit },
                    cancellationToken);
                if (recalled.Count > 0)
                    conversationHistory.Add(MemoryRecallFormatter.Format(recalled));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
            {
                await _ui.ShowMessageAsync($"[yellow]Memory recall unavailable: {Escape(ex.Message)}[/]");
            }
        }
        
        try
        {
            if (_useOrchestrator)
            {
                await RunOrchestratedConversationAsync(topic, conversationHistory, currentMessage, cancellationToken);
            }
            else
            {
                await RunRoundRobinConversationAsync(conversationHistory, currentMessage, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            await TryAutoSaveAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task RunOrchestratedConversationAsync(string topic, List<string> conversationHistory, string currentMessage, CancellationToken cancellationToken)
    {
        int totalTurns = 0;
        int maxTotalTurns = _maxTurns * _personas.Count; // Total individual turns allowed
        int consensusAttempts = 0;
        int maxConsensusAttempts = Math.Max(1, maxTotalTurns);
        int failedAttempts = 0;
        var terminationReason = ConversationTerminationReason.TurnBudgetExhausted;

        while (totalTurns < maxTotalTurns)
        {
            if (_ui.StopRequested)
            {
                terminationReason = ConversationTerminationReason.UserStopped;
                break;
            }

            try
            {
                AgentPersona? selectedPersona = null;
                string response = string.Empty;

                await _ui.RunWithStatusAsync("Orchestrating...", async () =>
                {
                    // Ask orchestrator who should speak next
                    var turnsRemaining = maxTotalTurns - totalTurns;
                    await _ui.SetStatusAsync($"Orchestrator selecting next speaker (Turns remaining: {turnsRemaining})...");

                    selectedPersona = await _orchestrator!.SelectNextSpeakerAsync(currentMessage, conversationHistory, turnsRemaining, cancellationToken);

                    if (selectedPersona != null)
                    {
                        await _ui.SetStatusAsync($"{Escape(selectedPersona.Name)} is thinking...");
                        response = await StreamResponseAsync(selectedPersona, currentMessage, conversationHistory, cancellationToken);
                    }
                });

                if (selectedPersona == null)
                {
                    consensusAttempts++;
                    var consensus = await VerifyConsensusAsync(currentMessage, conversationHistory, cancellationToken);
                    if (consensus.Reached)
                    {
                        terminationReason = ConversationTerminationReason.ConsensusReached;
                        await _ui.ShowRuleAsync("Consensus reached");
                        await _ui.ShowMessageAsync(
                            "\n[bold green]✅ All personas agree that the conversation can conclude.[/]");
                        break;
                    }

                    if (consensusAttempts >= maxConsensusAttempts)
                    {
                        terminationReason = ConversationTerminationReason.ConsensusBudgetExhausted;
                        await _ui.ShowRuleAsync("Consensus budget exhausted");
                        await _ui.ShowMessageAsync(
                            $"\n[yellow]⚠️  Consensus was not reached after {consensusAttempts} attempt(s). " +
                            "The conversation ended with unresolved concerns.[/]");
                        break;
                    }

                    currentMessage = consensus.FollowUpMessage;
                    await _ui.ShowMessageAsync(
                        $"\n[yellow]⚠️  Consensus has not been reached. The orchestrator will request another contribution.[/]\n" +
                        $"{Escape(consensus.FollowUpMessage)}");
                    continue;
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
                    await RunEditorIntervention(conversationHistory, cancellationToken);
                }

                // Fact Checker
                if (_factChecker != null)
                {
                    // Check the last message
                    await _factChecker.CheckAsync(response, cancellationToken);
                }

                // Context Summarization
                if (_contextSummarization && conversationHistory.Count > 15)
                {
                    await SummarizeHistoryAsync(conversationHistory, cancellationToken);
                }

                // Interactive Mode Check
                if (_interactiveMode)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var (action, message) = await _ui.GetUserInterventionAsync();
                    if (action == "quit")
                    {
                        terminationReason = ConversationTerminationReason.UserStopped;
                        break;
                    }
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
                System.Diagnostics.Trace.WriteLine($"[Orchestrator] Operation canceled: {ex.Message}", "Info");
                throw;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Orchestrator] Operation timed out: {ex.Message}", "Warning");
                await _ui.ShowErrorAsync("[red]❌ Operation timed out.[/]");
                totalTurns++;
            }
            catch (Exception ex) when (
                ex is StackOverflowException ||
                ex is OutOfMemoryException
            )
            {
                System.Diagnostics.Trace.WriteLine($"[Orchestrator] Critical error: {ex.GetType().Name} - {ex.Message}", "Error");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Orchestrator] Unexpected error: {ex.GetType().Name} - {ex.Message}", "Error");
                await _ui.ShowErrorAsync("[red]❌ An unexpected error occurred.[/]");
                failedAttempts++;
                if (failedAttempts >= maxTotalTurns)
                {
                    terminationReason = ConversationTerminationReason.FailureBudgetExhausted;
                    break;
                }
            }

            await _ui.ShowRuleAsync();

            cancellationToken.ThrowIfCancellationRequested();
        }

        _ui.TerminationReason = terminationReason;
        await _ui.ShowRuleAsync("Conversation Ended");
        await _ui.ShowMessageAsync($"\n{FormatTerminationMessage(terminationReason, maxTotalTurns)}");

        if (_dataExtractor != null)
        {
            await _dataExtractor.ExtractAndSaveAsync(conversationHistory, cancellationToken);
        }

        await TryExtractMemoryAsync(topic, conversationHistory, cancellationToken);
        await TryAutoSaveAsync(cancellationToken);
    }

    private static string FormatTerminationMessage(ConversationTerminationReason reason, int maxTotalTurns) =>
        reason switch
        {
            ConversationTerminationReason.ConsensusReached => "[bold green]✅ Consensus reached. Conversation ended successfully.[/]",
            ConversationTerminationReason.UserStopped => "[yellow]Conversation manually ended by user.[/]",
            ConversationTerminationReason.FailureBudgetExhausted => "[red]❌ Conversation ended after repeated failures.[/]",
            ConversationTerminationReason.ConsensusBudgetExhausted => "[yellow]⚠️ Consensus budget exhausted. Conversation ended with unresolved concerns.[/]",
            _ => $"[yellow]⏱️ Maximum turns ({maxTotalTurns}) reached. Conversation ended.[/]"
        };

    private async Task<(bool Reached, string FollowUpMessage)> VerifyConsensusAsync(
        string currentMessage,
        List<string> conversationHistory,
        CancellationToken cancellationToken)
    {
        await _ui.SetStatusAsync("Personas are checking consensus...");
        var assessments = await Task.WhenAll(
            _personas.Select(async persona =>
            {
                try
                {
                    var response = await persona.AssessConsensusAsync(
                        currentMessage, conversationHistory, cancellationToken);
                    var lines = response
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var vote = lines.FirstOrDefault(line =>
                        line.StartsWith("CONSENSUS:", StringComparison.OrdinalIgnoreCase));
                    var agrees = vote is not null &&
                        vote.Contains("YES", StringComparison.OrdinalIgnoreCase);
                    var reason = lines.FirstOrDefault(line =>
                        line.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase))
                        ?? "No rationale provided.";
                    return (persona.Name, Agrees: agrees, Reason: reason);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return (persona.Name, Agrees: false, Reason: $"Consensus check failed: {ex.Message}");
                }
            }));

        foreach (var assessment in assessments)
        {
            await _ui.ShowMessageAsync(
                $"[dim]Consensus — {Escape(assessment.Name)}: " +
                $"{(assessment.Agrees ? "AGREE" : "REVISE")} — {Escape(assessment.Reason)}[/]");
        }

        var dissent = assessments.Where(assessment => !assessment.Agrees).ToList();
        if (dissent.Count == 0)
        {
            return (true, string.Empty);
        }

        var concerns = string.Join(
            "\n",
            dissent.Select(assessment => $"- {assessment.Name}: {assessment.Reason}"));
        return (
            false,
            $"The consensus check found these unresolved concerns:\n{concerns}\n" +
            "Address the concerns in the document before proposing completion again.");
    }

    private async Task RunRoundRobinConversationAsync(List<string> conversationHistory, string currentMessage, CancellationToken cancellationToken)
    {
        int totalTurns = 0;
        for (int turn = 0; turn < _maxTurns; turn++)
        {
            foreach (var persona in _personas)
            {
                if (_ui.StopRequested)
                {
                    return;
                }

                try
                {
                    string response = string.Empty;

                    if (_showThinking)
                    {
                        await _ui.RunWithStatusAsync($"{Escape(persona.Name)} is thinking...", async () =>
                        {
                            response = await StreamResponseAsync(persona, currentMessage, conversationHistory, cancellationToken);
                        });
                    }
                    else
                    {
                        response = await StreamResponseAsync(persona, currentMessage, conversationHistory, cancellationToken);
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
                        await RunEditorIntervention(conversationHistory, cancellationToken);
                    }

                    // Fact Checker
                    if (_factChecker != null)
                    {
                        await _factChecker.CheckAsync(response, cancellationToken);
                    }

                    // Context Summarization
                    if (_contextSummarization && conversationHistory.Count > 15)
                    {
                        await SummarizeHistoryAsync(conversationHistory, cancellationToken);
                    }

                    // Interactive Mode Check
                    if (_interactiveMode)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var (action, message) = await _ui.GetUserInterventionAsync();
                        if (action == "quit")
                        {
                            await _ui.ShowMessageAsync($"\n[yellow]Conversation manually ended by user.[/]");
                            await TryAutoSaveAsync(cancellationToken);
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
                            await _dataExtractor.ExtractAndSaveAsync(conversationHistory, cancellationToken);
                        }

                        await TryExtractMemoryAsync(_currentTopic, conversationHistory, cancellationToken);
                        await TryAutoSaveAsync(cancellationToken);
                        return;
                    }
                }
                catch (OperationCanceledException ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[RoundRobin] Operation canceled: {ex.Message}", "Info");
                    throw;
                    }
                    catch (TimeoutException ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[RoundRobin] Operation timed out: {ex.Message}", "Warning");
                        await _ui.ShowErrorAsync("[red]❌ Operation timed out.[/]");
                    }
                    catch (Exception ex) when (
                        ex is StackOverflowException ||
                        ex is OutOfMemoryException
                    )
                    {
                        System.Diagnostics.Trace.WriteLine($"[RoundRobin] Critical error: {ex.GetType().Name} - {ex.Message}", "Error");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[RoundRobin] Unexpected error: {ex.GetType().Name} - {ex.Message}", "Error");
                        await _ui.ShowErrorAsync("[red]❌ An unexpected error occurred.[/]");
                    }
            }

            await _ui.ShowRuleAsync();
        }

        await _ui.ShowRuleAsync("Max Turns Reached");
        await _ui.ShowMessageAsync($"\n[yellow]⏱️  Maximum turns ({_maxTurns}) reached. Conversation ended.[/]");

        if (_dataExtractor != null)
        {
            await _dataExtractor.ExtractAndSaveAsync(conversationHistory, cancellationToken);
        }

        await TryExtractMemoryAsync(_currentTopic, conversationHistory, cancellationToken);
        await TryAutoSaveAsync(cancellationToken);
    }

    private async Task TryExtractMemoryAsync(
        string topic,
        List<string> conversationHistory,
        CancellationToken cancellationToken)
    {
        if (_memoryExtractor is null || _memoryStore is null || string.IsNullOrWhiteSpace(topic))
            return;

        try
        {
            var content = await _memoryExtractor.ExtractAsync(topic, conversationHistory, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
                return;

            await _memoryStore.AddAsync(new MemoryDto
            {
                Content = content,
                Source = $"conversation:{topic}",
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "successful-conversation"
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            await _ui.ShowMessageAsync($"[yellow]Memory extraction unavailable: {Escape(ex.Message)}[/]");
        }
    }

    private async Task<string> StreamResponseAsync(
        AgentPersona persona,
        string currentMessage,
        List<string> conversationHistory,
        CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        var lastDocumentPreview = persona.GetDocumentPreview();
        try
        {
            await foreach (var chunk in persona.RespondStreamingAsync(
                currentMessage,
                conversationHistory,
                cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                response.Append(chunk);
                await _ui.ShowAgentResponseChunkAsync(persona.Name, chunk);

                var documentPreview = persona.GetDocumentPreview();
                if (!string.Equals(documentPreview, lastDocumentPreview, StringComparison.Ordinal))
                {
                    lastDocumentPreview = documentPreview;
                    await _ui.ShowDocumentPreviewAsync(documentPreview);
                }
            }
        }
        catch
        {
            if (response.Length > 0)
                await _ui.ShowAgentResponseAsync(persona.Name, response.ToString());
            throw;
        }

        return response.ToString();
    }

    private async Task RunEditorIntervention(List<string> conversationHistory, CancellationToken cancellationToken)
    {
        await _ui.ShowRuleAsync("Editor Review");
        await _ui.ShowMessageAsync("\n[magenta]✂️  Refining document for clarity and conciseness...[/]");

        try
        {
            // Build context from recent conversation
            var countToTake = Math.Min(conversationHistory.Count, 6);
            var sbContext = new StringBuilder();
            int startIndex = conversationHistory.Count - countToTake;
            for (int i = 0; i < countToTake; i++)
            {
                if (i > 0) sbContext.Append('\n');
                sbContext.Append(conversationHistory[startIndex + i]);
            }
            var contextSummary = sbContext.Length > 0
                ? sbContext.ToString()
                : "No recent conversation";

            string editorResponse = string.Empty;
            await _ui.RunWithStatusAsync("Editor is reviewing...", async () =>
            {
                editorResponse = await _editor!.ReviewAndEditAsync(contextSummary, cancellationToken);
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
        catch (InvalidOperationException)
        {
            await _ui.ShowMessageAsync("[yellow]⚠️  Editor review skipped (invalid operation).[/]");
        }
        catch (TimeoutException)
        {
            await _ui.ShowMessageAsync("[yellow]⚠️  Editor review skipped (timeout).[/]");
        }

        await _ui.ShowMessageAsync("");
    }

    private bool IsConversationComplete(string response, int turn)
    {
        // For round-robin mode only: very conservative early completion
        // Require at least 80% of max turns before allowing early conclusion
        var minTurnsBeforeConclusion = Math.Max(6, (int)(_maxTurns * 0.8));

        if (turn < minTurnsBeforeConclusion) return false;

        // Only match extremely explicit conclusion statements
        var completionIndicators = new[]
        {
            "this conversation is now complete",
            "our work here is finished",
            "ready to end this discussion"
        };

        var lowerResponse = response.ToLowerInvariant();
        return completionIndicators.Any(indicator => lowerResponse.Contains(indicator));
    }

    private async Task TryAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var path = await _doc.SaveToFileAsync("conversation.md", cancellationToken);

            await _ui.ShowMessageAsync($"[green]✓ Auto-saved collaborative document ({Escape(path)})[/]");
        }
        catch (System.IO.IOException)
        {
            await _ui.ShowErrorAsync("[yellow]⚠️  Auto-save failed (IO error).[/]");
        }
        catch (UnauthorizedAccessException)
        {
            await _ui.ShowErrorAsync("[yellow]⚠️  Auto-save failed (access denied).[/]");
        }
    }

    private async Task SummarizeHistoryAsync(List<string> conversationHistory, CancellationToken cancellationToken)
    {
        if (_personas.Count == 0) return;

        int countToSummarize = Math.Min(conversationHistory.Count, 10);
        var sb = new StringBuilder();
        for (int i = 0; i < countToSummarize; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(conversationHistory[i]);
        }
        var historyText = sb.ToString();

        try
        {
            await _ui.RunWithStatusAsync("Summarizing context...", async () =>
            {
                string summary = string.Empty;

                if (_orchestrator != null)
                {
                    summary = await _orchestrator.SummarizeAsync(historyText, cancellationToken);
                }
                else
                {
                    // Fallback: truncate oldest messages to save context
                    int removeCount = Math.Min(countToSummarize, conversationHistory.Count);
                    if (removeCount > 0)
                    {
                        conversationHistory.RemoveRange(0, removeCount);
                        conversationHistory.Insert(0, $"[... {removeCount} older messages truncated to save context ...]");
                    }
                    return;
                }

                if (!string.IsNullOrWhiteSpace(summary))
                {
                    // Ensure we don't remove more than available if list shrank (unlikely but safe)
                    int removeCount = Math.Min(conversationHistory.Count, countToSummarize);
                    conversationHistory.RemoveRange(0, removeCount);
                    conversationHistory.Insert(0, $"[Summary of previous turns]: {summary}");
                    await _ui.ShowMessageAsync("[dim]History summarized to save tokens.[/]");
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            await _ui.ShowErrorAsync("[dim red]Summarization failed.[/]");
        }
    }

    // Helper to escape markup since we are using Spectre Console conventions in strings still
    private static string Escape(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
        // Note: Generic escaping might be needed if UI implementation relies on markup.
        // For now we assume the string passing uses Spectre-like markup or plain text.
        // If the UI is Blazor, we might need to handle this differently.
        // Ideally we should pass plain text and let the UI handle formatting, or use a rich text model.
        // For simplicity, we keep the markup strings and let the implementations handle them.
    }
}
