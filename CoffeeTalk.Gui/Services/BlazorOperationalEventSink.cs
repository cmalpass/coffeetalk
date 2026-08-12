using CoffeeTalk.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoffeeTalk.Gui.Services;

public sealed class BlazorOperationalEventSink : IOperationalEventSink
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
        if (operationalEvent.Exception is not null)
        {
            _logger.LogError(
                operationalEvent.Exception,
                "Operational event {EventKind} for {Operation}; attempt {Attempt}/{MaxRetries}; decision {Decision}; reason {Reason}",
                operationalEvent.Kind,
                operationalEvent.Operation,
                operationalEvent.Attempt,
                operationalEvent.MaxRetries,
                operationalEvent.Decision,
                operationalEvent.Reason);
        }
        else
        {
            _logger.LogInformation(
                "Operational event {EventKind} for {Operation}; attempt {Attempt}/{MaxRetries}; decision {Decision}; reason {Reason}",
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
            _ => throw new ArgumentOutOfRangeException(nameof(operationalEvent), operationalEvent.Kind, "Unknown operational event kind.")
        };

        _ui.ShowMessageAsync(message).GetAwaiter().GetResult();
    }
}
