namespace CoffeeTalk.Core.Interfaces;

public enum OperationalEventKind
{
    RetryAttempt,
    RetryTerminalFailure,
    OrchestratorDecision,
    OperationFailure
}

public sealed record OperationalEvent(
    OperationalEventKind Kind,
    string Operation,
    int? Attempt = null,
    int? MaxRetries = null,
    int? DelaySeconds = null,
    string? Decision = null,
    string? Reason = null)
{
    public Exception? Exception { get; init; }
}

public interface IOperationalEventSink
{
    void Publish(OperationalEvent operationalEvent);
}

public sealed class NullOperationalEventSink : IOperationalEventSink
{
    public static NullOperationalEventSink Instance { get; } = new();

    private NullOperationalEventSink()
    {
    }

    public void Publish(OperationalEvent operationalEvent)
    {
    }
}

public interface IRetryService
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default);
}
