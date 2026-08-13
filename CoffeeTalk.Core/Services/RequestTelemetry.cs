using CoffeeTalk.Core.Interfaces;
using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace CoffeeTalk.Services;

internal sealed class RequestTelemetry
{
    private readonly IOperationalEventSink _eventSink;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly string _requestId = Guid.NewGuid().ToString("N")[..8];
    private readonly string _operation;
    private readonly int _promptCharacters;
    private int _outputCharacters;
    private long? _firstTokenMilliseconds;
    private UsageDetails? _usage;

    public RequestTelemetry(
        IOperationalEventSink eventSink,
        string operation,
        string prompt)
    {
        _eventSink = eventSink;
        _operation = operation;
        _promptCharacters = prompt.Length;
        _eventSink.Publish(new OperationalEvent(
            OperationalEventKind.RequestStarted,
            operation,
            Reason: $"request={_requestId}")
        {
            RequestId = _requestId,
            PromptCharacters = _promptCharacters,
            EstimatedPromptTokens = EstimateTokens(_promptCharacters)
        });
    }

    public void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        _outputCharacters += text.Length;
        _firstTokenMilliseconds ??= _stopwatch.ElapsedMilliseconds;
    }

    public void PublishThinking(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _eventSink.Publish(new OperationalEvent(
            OperationalEventKind.RequestThinking,
            _operation,
            Reason: $"request={_requestId}; {text.Trim()} ")
        {
            RequestId = _requestId
        });
    }

    public void Complete(UsageDetails? usage = null)
    {
        _usage = usage;
        _eventSink.Publish(new OperationalEvent(
            OperationalEventKind.RequestCompleted,
            _operation,
            Reason: $"request={_requestId}")
        {
            RequestId = _requestId,
            PromptCharacters = _promptCharacters,
            EstimatedPromptTokens = EstimateTokens(_promptCharacters),
            OutputCharacters = _outputCharacters,
            EstimatedOutputTokens = EstimateTokens(_outputCharacters),
            InputTokens = _usage?.InputTokenCount,
            OutputTokens = _usage?.OutputTokenCount,
            TotalTokens = _usage?.TotalTokenCount,
            DurationMilliseconds = _stopwatch.ElapsedMilliseconds,
            FirstTokenMilliseconds = _firstTokenMilliseconds
        });
    }

    public void Fail(Exception exception)
    {
        _eventSink.Publish(new OperationalEvent(
            OperationalEventKind.RequestFailed,
            _operation,
            Reason: $"request={_requestId}; {exception.Message}")
        {
            RequestId = _requestId,
            PromptCharacters = _promptCharacters,
            EstimatedPromptTokens = EstimateTokens(_promptCharacters),
            OutputCharacters = _outputCharacters,
            EstimatedOutputTokens = EstimateTokens(_outputCharacters),
            InputTokens = _usage?.InputTokenCount,
            OutputTokens = _usage?.OutputTokenCount,
            TotalTokens = _usage?.TotalTokenCount,
            DurationMilliseconds = _stopwatch.ElapsedMilliseconds,
            FirstTokenMilliseconds = _firstTokenMilliseconds,
            Exception = exception
        });
    }

    private static int EstimateTokens(int characters) =>
        characters == 0 ? 0 : (characters + 3) / 4;
}
