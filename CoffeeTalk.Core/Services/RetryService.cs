using System.Net;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public sealed class RetryService : IRetryService
{
    private readonly RetryConfig _config;
    private readonly IOperationalEventSink _eventSink;

    public RetryService(RetryConfig? config, IOperationalEventSink? eventSink = null)
    {
        _config = config ?? new RetryConfig();
        _eventSink = eventSink ?? NullOperationalEventSink.Instance;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        int retryCount = 0;
        int delaySeconds = _config.InitialDelaySeconds;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation(cancellationToken);
            }
            catch (HttpRequestException ex) when (IsRateLimitHttpException(ex))
            {
                if (!RegisterRetry(operationName, ref retryCount, delaySeconds))
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                delaySeconds = (int)(delaySeconds * _config.BackoffMultiplier);
            }
            catch (Exception ex) when (IsRateLimitException(ex))
            {
                if (!RegisterRetry(operationName, ref retryCount, delaySeconds))
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                delaySeconds = (int)(delaySeconds * _config.BackoffMultiplier);
            }
        }
    }

    private bool RegisterRetry(string operationName, ref int retryCount, int delaySeconds)
    {
        retryCount++;
        if (retryCount > _config.MaxRetries)
        {
            _eventSink.Publish(new OperationalEvent(
                OperationalEventKind.RetryTerminalFailure,
                operationName,
                retryCount - 1,
                _config.MaxRetries));
            return false;
        }

        _eventSink.Publish(new OperationalEvent(
            OperationalEventKind.RetryAttempt,
            operationName,
            retryCount,
            _config.MaxRetries,
            delaySeconds));

        return true;
    }

    private static bool IsRateLimitHttpException(HttpRequestException ex) =>
        ex.StatusCode == HttpStatusCode.TooManyRequests;

    private static bool IsRateLimitException(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("429") ||
               message.Contains("rate limit") ||
               message.Contains("too many requests") ||
               (ex.InnerException != null && IsRateLimitException(ex.InnerException));
    }
}
