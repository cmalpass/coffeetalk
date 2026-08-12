using CoffeeTalk.Models;
using CoffeeTalk.Services;
using Microsoft.Extensions.Logging;

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
                await activeTask.ConfigureAwait(false);

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
            await previousTask.ConfigureAwait(false);

        _ui.ResetForNewConversation();
        await RunAsync(topic, personas, cts);
    }

    private async Task RunAsync(string topic, List<PersonaConfig> personas, CancellationTokenSource cts)
    {
        try
        {
            var pipeline = await _pipelineBuilder.BuildAsync(_appState.Settings, topic, personas, cts.Token);
            await pipeline.CreateConversation(_ui).StartConversationAsync(topic, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during conversation orchestration");
            await _ui.ShowErrorAsync("An error occurred while starting the conversation. Please check your configuration and try again.");
        }
        finally
        {
            lock (_gate)
            {
                try
                {
                    var messages = _ui.GetMessagesSnapshot();
                    _history.Add(new ConversationRecord
                    {
                        Topic = topic,
                        StartedAt = _ui.ConversationStartedAt ?? DateTime.Now,
                        CompletedAt = DateTime.Now,
                        Status = cts.IsCancellationRequested || _ui.StopRequested ? "Stopped" : "Completed",
                        MessageCount = messages.Count,
                        Personas = _ui.ConversationParticipants.ToList(),
                        Messages = messages.ToList(),
                        DocumentContent = _ui.DocumentMarkdown
                    });
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "Failed to write conversation history for {Topic}", topic);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "Failed to persist conversation history for {Topic}", topic);
                }
                if (ReferenceEquals(_cts, cts))
                {
                    _ui.EndConversation();
                    _conversationTask = null;
                    _cts = null;
                }
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
