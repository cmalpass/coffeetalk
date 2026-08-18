using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public class RetryServiceTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesRetryAndTerminalEvents()
    {
        var sink = new RecordingEventSink();
        var service = new RetryService(new RetryConfig
        {
            InitialDelaySeconds = 0,
            MaxRetries = 1,
            BackoffMultiplier = 2
        }, sink);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.ExecuteAsync<string>(
                _ => Task.FromException<string>(new HttpRequestException("rate limit")),
                "Test operation"));

        Assert.Collection(
            sink.Events,
            retry => Assert.Equal(OperationalEventKind.RetryAttempt, retry.Kind),
            terminal => Assert.Equal(OperationalEventKind.RetryTerminalFailure, terminal.Kind));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotConvertCancellationToRetryEvent()
    {
        var sink = new RecordingEventSink();
        var service = new RetryService(new RetryConfig { InitialDelaySeconds = 0 }, sink);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(
                _ => Task.FromResult("unused"),
                "Test operation",
                cancellationToken: cancellation.Token));

        Assert.Empty(sink.Events);
    }

    private sealed class RecordingEventSink : IOperationalEventSink
    {
        public List<OperationalEvent> Events { get; } = new();

        public void Publish(OperationalEvent operationalEvent) => Events.Add(operationalEvent);
    }
}
