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
        CancellationToken cancellationToken = default,
        Func<CancellationToken, Task>? beforeRetry = null)
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
            catch (HttpRequestException ex) when (IsTransientHttpException(ex))
            {
                if (!RegisterRetry(operationName, ref retryCount, delaySeconds))
                {
                    throw;
                }

                if (beforeRetry is not null)
                    await beforeRetry(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                delaySeconds = NextDelaySeconds(delaySeconds);
            }
            catch (Exception ex) when (IsRateLimitException(ex))
            {
                if (!RegisterRetry(operationName, ref retryCount, delaySeconds))
                {
                    throw;
                }

                if (beforeRetry is not null)
                    await beforeRetry(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                delaySeconds = NextDelaySeconds(delaySeconds);
            }
        }
    }

    /// <summary>
    /// Computes the next backoff delay in seconds after a retry, applying the configured
    /// multiplier and capping the result at MaxDelaySeconds. Overflow is avoided by
    /// computing the product in <see cref="double"/> and clamping to an <see cref="int"/>.
    /// </summary>
    private int NextDelaySeconds(int delaySeconds)
    {
        var next = delaySeconds * _config.BackoffMultiplier;

        if (double.IsInfinity(next) || next > int.MaxValue)
            return _config.MaxDelaySeconds > 0 ? _config.MaxDelaySeconds : int.MaxValue;

        var rounded = (int)Math.Round(next, MidpointRounding.AwayFromZero);

        if (_config.MaxDelaySeconds > 0 && rounded > _config.MaxDelaySeconds)
            return _config.MaxDelaySeconds;

        return rounded;
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

    /// <summary>
    /// Returns true for statuses that are safe to retry: 429 (rate limit), 408 (request
    /// timeout), and 5xx server errors.
    /// </summary>
    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        int code = (int)statusCode;
        return code == 429 ||
               code == 408 ||
               (code >= 500 && code <= 599);
    }

    private static bool IsTransientHttpException(HttpRequestException ex)
    {
        if (ex.StatusCode is not null)
            return IsRetryableStatusCode(ex.StatusCode.Value);

        // No status code: a transient network-level failure (connection refused/reset, DNS, etc.).
        return true;
    }

    private static bool IsRateLimitException(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("429") ||
               message.Contains("rate limit") ||
               message.Contains("too many requests") ||
               (ex.InnerException != null && IsRateLimitException(ex.InnerException));
    }
}
