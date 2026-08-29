using CoffeeTalk.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoffeeTalk.Gui.Services;

public sealed partial class BlazorOperationalEventSink : IOperationalEventSink
{
    private readonly ILogger<BlazorOperationalEventSink> _logger;
    private readonly BlazorUserInterface _ui;

    public BlazorOperationalEventSink(
        ILogger<BlazorOperationalEventSink> logger,
        BlazorUserInterface ui)
    {
        _logger = logger;
        _ui = ui;
    }

    public void Publish(OperationalEvent operationalEvent)
    {
        if (operationalEvent.Kind == OperationalEventKind.RequestFallback)
        {
            LogFallback(
                _logger,
                operationalEvent.Exception,
                operationalEvent.Operation,
                operationalEvent.RequestId,
                operationalEvent.Reason);
        }
        else if (operationalEvent.Exception is not null)
        {
            LogOperationalError(
                _logger,
                operationalEvent.Exception,
                operationalEvent.Kind,
                operationalEvent.Operation,
                operationalEvent.Attempt,
                operationalEvent.MaxRetries,
                operationalEvent.Decision,
                operationalEvent.Reason);
        }
        else
        {
            LogOperationalInfo(
                _logger,
                operationalEvent.Kind,
                operationalEvent.Operation,
                operationalEvent.Attempt,
                operationalEvent.MaxRetries,
                operationalEvent.Decision,
                operationalEvent.Reason);
        }

        var message = operationalEvent.Kind switch
        {
            OperationalEventKind.RetryAttempt =>
                $"Retrying {operationalEvent.Operation} (attempt {operationalEvent.Attempt}/{operationalEvent.MaxRetries}) after {operationalEvent.DelaySeconds}s.",
            OperationalEventKind.RetryTerminalFailure =>
                $"{operationalEvent.Operation} failed after {operationalEvent.MaxRetries} retries.",
            OperationalEventKind.OrchestratorDecision =>
                $"Orchestrator: {operationalEvent.Decision}" +
                (string.IsNullOrWhiteSpace(operationalEvent.Reason) ? string.Empty : $" ({operationalEvent.Reason})"),
            OperationalEventKind.OperationFailure =>
                $"{operationalEvent.Operation} failed.",
            OperationalEventKind.RequestStarted =>
                $"Started {operationalEvent.Operation} [{operationalEvent.RequestId}] — context ~{operationalEvent.EstimatedPromptTokens} tokens ({operationalEvent.PromptCharacters} chars).",
            OperationalEventKind.RequestThinking =>
                $"Thinking {operationalEvent.Operation} [{operationalEvent.RequestId}]: {operationalEvent.Reason}",
            OperationalEventKind.RequestCompleted =>
                $"Completed {operationalEvent.Operation} [{operationalEvent.RequestId}] in {FormatDuration(operationalEvent.DurationMilliseconds)} — first output {FormatDuration(operationalEvent.FirstTokenMilliseconds)}, context {FormatTokenCount(operationalEvent.InputTokens, operationalEvent.EstimatedPromptTokens)} tokens, output {FormatTokenCount(operationalEvent.OutputTokens, operationalEvent.EstimatedOutputTokens)} tokens ({operationalEvent.OutputCharacters} chars), total {FormatTokenCount(operationalEvent.TotalTokens, null)}.",
            OperationalEventKind.RequestFailed =>
                $"Failed {operationalEvent.Operation} [{operationalEvent.RequestId}] after {FormatDuration(operationalEvent.DurationMilliseconds)} — {operationalEvent.Reason}",
            OperationalEventKind.RequestFallback =>
                $"Falling back to buffered {operationalEvent.Operation} [{operationalEvent.RequestId}] — {operationalEvent.Reason}",
            OperationalEventKind.ToolStarted =>
                $"Tool started {operationalEvent.Operation} [{operationalEvent.RequestId}] — arguments {operationalEvent.ArgumentCharacters} chars.",
            OperationalEventKind.ToolCompleted =>
                $"Tool completed {operationalEvent.Operation} [{operationalEvent.RequestId}] in {FormatDuration(operationalEvent.DurationMilliseconds)} — result {operationalEvent.ResultCharacters} chars.",
            OperationalEventKind.ToolFailed =>
                $"Tool failed {operationalEvent.Operation} [{operationalEvent.RequestId}] after {FormatDuration(operationalEvent.DurationMilliseconds)} — {operationalEvent.Reason}",
            OperationalEventKind.DataExtractionRetry =>
                $"{operationalEvent.Operation} produced invalid JSON; re-prompting (attempt {operationalEvent.Attempt}/{operationalEvent.MaxRetries}).",
            OperationalEventKind.DataExtractionFailed =>
                $"{operationalEvent.Operation} failed — the model output was not valid JSON and no data file was written.",
            _ => throw new ArgumentOutOfRangeException(nameof(operationalEvent), operationalEvent.Kind, "Unknown operational event kind.")
        };

        if (operationalEvent.Kind is OperationalEventKind.RequestStarted
            or OperationalEventKind.RequestThinking
            or OperationalEventKind.RequestCompleted
            or OperationalEventKind.RequestFailed
            or OperationalEventKind.RequestFallback
            or OperationalEventKind.ToolStarted
            or OperationalEventKind.ToolCompleted
            or OperationalEventKind.ToolFailed)
        {
            var category = operationalEvent.Kind switch
            {
                OperationalEventKind.RequestStarted => "started",
                OperationalEventKind.RequestThinking => "thinking",
                OperationalEventKind.RequestCompleted => "completed",
                OperationalEventKind.RequestFailed => "failed",
                OperationalEventKind.RequestFallback => "fallback",
                OperationalEventKind.ToolStarted => "started",
                OperationalEventKind.ToolCompleted => "completed",
                OperationalEventKind.ToolFailed => "failed",
                _ => "event"
            };
            _ = _ui.ShowTelemetryAsync(operationalEvent.RequestId ?? "unknown", category, message);
        }
        else
        {
            _ = _ui.ShowMessageAsync(message);
        }
    }

    private static string FormatDuration(long? milliseconds) =>
        milliseconds is null
            ? "n/a"
            : milliseconds < 1000
                ? $"{milliseconds}ms"
                : $"{milliseconds.Value / 1000d:0.0}s";

    private static string FormatTokenCount(long? actual, int? estimate) =>
        actual is not null ? $"{actual} actual" : $"~{estimate} estimated";

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Operational fallback for {Operation}; request {RequestId}; reason {Reason}")]
    private static partial void LogFallback(
        ILogger logger,
        Exception? exception,
        string operation,
        string? requestId,
        string? reason);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Operational event {EventKind} for {Operation}; attempt {Attempt}/{MaxRetries}; decision {Decision}; reason {Reason}")]
    private static partial void LogOperationalError(
        ILogger logger,
        Exception exception,
        OperationalEventKind eventKind,
        string operation,
        int? attempt,
        int? maxRetries,
        string? decision,
        string? reason);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Operational event {EventKind} for {Operation}; attempt {Attempt}/{MaxRetries}; decision {Decision}; reason {Reason}")]
    private static partial void LogOperationalInfo(
        ILogger logger,
        OperationalEventKind eventKind,
        string operation,
        int? attempt,
        int? maxRetries,
        string? decision,
        string? reason);
}
