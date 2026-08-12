using CoffeeTalk.Core.Interfaces;
using Spectre.Console;

namespace CoffeeTalk.Services;

public sealed class CliOperationalEventSink : IOperationalEventSink
{
    public void Publish(OperationalEvent operationalEvent)
    {
        var detail = operationalEvent.Kind switch
        {
            OperationalEventKind.RetryAttempt =>
                $"{operationalEvent.Operation}: retry {operationalEvent.Attempt}/{operationalEvent.MaxRetries}, waiting {operationalEvent.DelaySeconds}s",
            OperationalEventKind.RetryTerminalFailure =>
                $"{operationalEvent.Operation}: retries exhausted after {operationalEvent.MaxRetries} retries",
            OperationalEventKind.OrchestratorDecision =>
                $"Orchestrator: {operationalEvent.Decision} ({operationalEvent.Reason})",
            OperationalEventKind.OperationFailure =>
                $"{operationalEvent.Operation}: operation failed",
            _ => operationalEvent.Operation
        };

        AnsiConsole.MarkupLine($"[dim]Operational event: {Markup.Escape(detail)}[/]");
    }
}
