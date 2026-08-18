using CoffeeTalk.Models;
using CoffeeTalk.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CoffeeTalk.Gui.Services;

public interface IConversationSessionService
{
    bool IsRunning { get; }
    void Start(string topic, IReadOnlyCollection<PersonaConfig> personas);
    void Cancel();
    Task LoadConversationAsync(ConversationRecord record);
}

public sealed class ConversationSessionService : IConversationSessionService, IDisposable
{
    private readonly AppState _appState;
    private readonly BlazorUserInterface _ui;
    private readonly ConversationHistoryService _history;
    private readonly ConversationPipelineBuilder _pipelineBuilder;
    private readonly ILogger<ConversationSessionService> _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _conversationTask;
    private bool _disposed;

    public ConversationSessionService(
        AppState appState,
        BlazorUserInterface ui,
        ConversationHistoryService history,
        ConversationPipelineBuilder pipelineBuilder,
        ILogger<ConversationSessionService> logger)
    {
        _appState = appState;
        _ui = ui;
        _history = history;
        _pipelineBuilder = pipelineBuilder;
        _logger = logger;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _conversationTask is { IsCompleted: false };
        }
    }

    public void Start(string topic, IReadOnlyCollection<PersonaConfig> personas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(personas);
        _logger.LogInformation(
            "Starting conversation for topic {Topic} with personas {Personas}",
            topic,
            string.Join(", ", personas.Select(persona => persona.Name)));

        CancellationTokenSource cts;
        Task? previousTask;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            previousTask = _conversationTask;
            _cts?.Cancel();
            cts = _cts = new CancellationTokenSource();
            _conversationTask = RunAfterPreviousAsync(previousTask, topic, personas.ToList(), cts);
        }
        _ui.BeginConversation(topic, personas.Select(persona => persona.Name).ToList());
        _ui.CancelIntervention(false);
    }

    public void Cancel()
    {
        lock (_gate)
            _cts?.Cancel();
        _ui.CancelIntervention();
    }

    public async Task LoadConversationAsync(ConversationRecord record)
    {
        while (true)
        {
            Task? activeTask;
            lock (_gate)
            {
                activeTask = _conversationTask;
                _cts?.Cancel();
            }
            _ui.CancelIntervention();

            if (activeTask is not null)
            {
                try
                {
                    await activeTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (activeTask.IsCanceled)
                {
                }
            }

            lock (_gate)
            {
                if (ReferenceEquals(_conversationTask, activeTask))
                {
                    _ui.LoadConversation(record);
                    return;
                }
            }
        }
    }

    private async Task RunAfterPreviousAsync(
        Task? previousTask,
        string topic,
        List<PersonaConfig> personas,
        CancellationTokenSource cts)
    {
        if (previousTask is not null)
        {
            try
            {
                await previousTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (previousTask.IsCanceled)
            {
            }
        }

        cts.Token.ThrowIfCancellationRequested();
        _ui.ResetForNewConversation();
        await RunAsync(topic, personas, cts);
    }

    private async Task RunAsync(string topic, List<PersonaConfig> personas, CancellationTokenSource cts)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Building conversation pipeline for {Topic}", topic);
            var pipeline = await _pipelineBuilder.BuildAsync(_appState.Settings, topic, personas, cts.Token);
            _logger.LogInformation(
                "Conversation pipeline built for {Topic}; orchestrator={HasOrchestrator}, personas={PersonaCount}",
                topic,
                pipeline.Orchestrator is not null,
                pipeline.Personas.Count);
            _logger.LogInformation("Entering agent conversation execution for {Topic}", topic);
            await pipeline.CreateConversation(_ui).StartConversationAsync(topic, cts.Token);
            _logger.LogInformation("Agent conversation execution completed for {Topic}", topic);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during conversation orchestration");
            await _ui.ShowErrorAsync($"Conversation failed: {ex.Message}");
        }
        finally
        {
            _logger.LogInformation(
                "Conversation task finished for {Topic} after {ElapsedSeconds:F1}s; cancelled={Cancelled}",
                topic,
                stopwatch.Elapsed.TotalSeconds,
                cts.IsCancellationRequested);
            ConversationRecord? record = null;
            lock (_gate)
            {
                var messages = _ui.GetMessagesSnapshot();
                var terminationReason = cts.IsCancellationRequested
                    ? ConversationTerminationReason.Cancelled
                    : _ui.TerminationReason == ConversationTerminationReason.Unknown
                        ? (_ui.StopRequested ? ConversationTerminationReason.UserStopped : ConversationTerminationReason.TurnBudgetExhausted)
                        : _ui.TerminationReason;
                record = new ConversationRecord
                {
                    Topic = topic,
                    StartedAt = _ui.ConversationStartedAt ?? DateTime.Now,
                    CompletedAt = DateTime.Now,
                    Status = terminationReason is ConversationTerminationReason.Cancelled or ConversationTerminationReason.UserStopped
                        ? "Stopped"
                        : "Completed",
                    MessageCount = messages.Count,
                    Personas = _ui.ConversationParticipants.ToList(),
                    Messages = messages.ToList(),
                    DocumentContent = _ui.DocumentMarkdown,
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["terminationReason"] = terminationReason.ToString()
                    }
                };
                if (ReferenceEquals(_cts, cts))
                {
                    _ui.EndConversation();
                    _conversationTask = null;
                    _cts = null;
                }
            }
            try
            {
                if (record is not null)
                    await _history.SaveAsync(record);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to write conversation history for {Topic}", topic);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Failed to persist conversation history for {Topic}", topic);
            }
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _cts?.Cancel();
        }
        _ui.CancelIntervention();
    }
}
