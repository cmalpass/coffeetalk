namespace CoffeeTalk.Core.Interfaces;

public enum OperationalEventKind
{
    RetryAttempt,
    RetryTerminalFailure,
    OrchestratorDecision,
    OperationFailure,
    RequestStarted,
    RequestThinking,
    RequestCompleted,
    RequestFailed,
    RequestFallback,
    ToolStarted,
    ToolCompleted,
    ToolFailed
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
    public string? RequestId { get; init; }
    public int? PromptCharacters { get; init; }
    public int? EstimatedPromptTokens { get; init; }
    public int? OutputCharacters { get; init; }
    public int? EstimatedOutputTokens { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public long? TotalTokens { get; init; }
    public long? DurationMilliseconds { get; init; }
    public long? FirstTokenMilliseconds { get; init; }
    public int? ThinkingCharacters { get; init; }
    public int? EstimatedThinkingTokens { get; init; }
    public long? ThinkingDurationMilliseconds { get; init; }
    public int? ArgumentCharacters { get; init; }
    public int? ResultCharacters { get; init; }
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "The cancellation token position is preserved for interface compatibility; callers use named arguments.")]
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, Task>? beforeRetry = null);
}
