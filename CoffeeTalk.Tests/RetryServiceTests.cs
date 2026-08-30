using System.Net;
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

    [Fact]
    public async Task ExecuteAsync_RetriesOnServerError()
    {
        var sink = new RecordingEventSink();
        var attempts = 0;
        var service = new RetryService(new RetryConfig
        {
            InitialDelaySeconds = 0,
            MaxRetries = 2,
            BackoffMultiplier = 2
        }, sink);

        var result = await service.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                    return Task.FromException<int>(new HttpRequestException("boom", null, HttpStatusCode.InternalServerError));
                return Task.FromResult(42);
            },
            "Test operation");

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
        Assert.Single(sink.Events);
        Assert.All(sink.Events, e => Assert.Equal(OperationalEventKind.RetryAttempt, e.Kind));
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnTransientNetworkError()
    {
        var sink = new RecordingEventSink();
        var attempts = 0;
        var service = new RetryService(new RetryConfig
        {
            InitialDelaySeconds = 0,
            MaxRetries = 2,
            BackoffMultiplier = 2
        }, sink);

        var result = await service.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                    return Task.FromException<int>(new HttpRequestException("connection refused"));
                return Task.FromResult(42);
            },
            "Test operation");

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetryOnClientError()
    {
        var sink = new RecordingEventSink();
        var attempts = 0;
        var service = new RetryService(new RetryConfig
        {
            InitialDelaySeconds = 0,
            MaxRetries = 5,
            BackoffMultiplier = 2
        }, sink);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.ExecuteAsync(
                _ =>
                {
                    attempts++;
                    return Task.FromException<int>(new HttpRequestException("bad request", null, HttpStatusCode.BadRequest));
                },
                "Test operation"));

        Assert.Equal(1, attempts);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void NextDelay_DoesNotOverflowAtExtremeBackoff()
    {
        // Even at an extreme attempt count the computation clamps to int.MaxValue
        // instead of overflowing to a negative value.
        var config = new RetryConfig { InitialDelaySeconds = int.MaxValue };
        var service = new RetryService(config);

        var value = InvokeNextDelay(service, int.MaxValue);

        Assert.True(value > 0, $"Backoff must remain positive, got {value}.");
    }

    [Fact]
    public void NextDelay_CapsBackoffAtMaxDelay()
    {
        var config = new RetryConfig { MaxDelaySeconds = 30, BackoffMultiplier = 4.0 };
        var service = new RetryService(config);

        var next = InvokeNextDelay(service, 10);

        Assert.Equal(30, next);
        Assert.True(next <= 30, $"Delay {next} exceeded cap.");
    }

    [Fact]
    public void NextDelay_StaysPositiveWhenMultiplierOverflows()
    {
        var config = new RetryConfig { MaxDelaySeconds = 0, BackoffMultiplier = 2.0 };
        var service = new RetryService(config);

        var next = InvokeNextDelay(service, int.MaxValue);

        Assert.True(next > 0, $"Backoff must remain positive, got {next}.");
        Assert.Equal(int.MaxValue, next);
    }

    [Fact]
    public void RetryConfig_MaxDelaySecondsDefaultsToSaneCap()
    {
        var config = new RetryConfig();
        Assert.Equal(600, config.MaxDelaySeconds);
    }

    private static int InvokeNextDelay(RetryService service, int delaySeconds)
    {
        var method = typeof(RetryService).GetMethod(
            "NextDelaySeconds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        return (int)method.Invoke(service, new object[] { delaySeconds })!;
    }

    private sealed class RecordingEventSink : IOperationalEventSink
    {
        public List<OperationalEvent> Events { get; } = new();

        public void Publish(OperationalEvent operationalEvent) => Events.Add(operationalEvent);
    }
}
