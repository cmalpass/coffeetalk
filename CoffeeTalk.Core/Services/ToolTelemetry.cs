using CoffeeTalk.Core.Interfaces;
using System.Diagnostics;

namespace CoffeeTalk.Services;

internal sealed class ToolTelemetry
{
    private readonly IOperationalEventSink _eventSink;
    private readonly string _operation;
    private readonly string _requestId = Guid.NewGuid().ToString("N")[..8];
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly int _argumentCharacters;

    public ToolTelemetry(IOperationalEventSink eventSink, string operation, string? arguments)
    {
        _eventSink = eventSink;
        _operation = operation;
        _argumentCharacters = arguments?.Length ?? 0;
        _eventSink.Publish(new OperationalEvent(OperationalEventKind.ToolStarted, operation, Reason: $"tool={_requestId}")
        {
            RequestId = _requestId,
            ArgumentCharacters = _argumentCharacters
        });
    }

    public void Complete(string? result)
    {
        _eventSink.Publish(new OperationalEvent(OperationalEventKind.ToolCompleted, _operation, Reason: $"tool={_requestId}")
        {
            RequestId = _requestId,
            ArgumentCharacters = _argumentCharacters,
            ResultCharacters = result?.Length ?? 0,
            DurationMilliseconds = _stopwatch.ElapsedMilliseconds
        });
    }

    public void Fail(Exception exception)
    {
        _eventSink.Publish(new OperationalEvent(
            OperationalEventKind.ToolFailed,
            _operation,
            Reason: $"tool={_requestId}; exception={exception.GetType().Name}")
        {
            RequestId = _requestId,
            ArgumentCharacters = _argumentCharacters,
            DurationMilliseconds = _stopwatch.ElapsedMilliseconds,
            Exception = exception
        });
    }
}
