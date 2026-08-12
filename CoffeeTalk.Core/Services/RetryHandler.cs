using System.Net;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public static class RetryHandler
{
    private static RetryConfig _config = new RetryConfig();

    public static void Configure(RetryConfig? config)
    {
        _config = config ?? new RetryConfig();
    }

    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName = "Operation")
    {
        int retryCount = 0;
        int delaySeconds = _config.InitialDelaySeconds;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (IsRateLimitHttpException(ex))
            {
                retryCount++;

                if (retryCount > _config.MaxRetries)
                {
                    System.Diagnostics.Trace.WriteLine($"[{operationName}] Failed after {_config.MaxRetries} retries due to rate limiting.", "Error");
                    throw;
                }

                System.Diagnostics.Trace.WriteLine($"[{operationName}] Rate limit hit (HTTP 429). Retry {retryCount}/{_config.MaxRetries} - waiting {delaySeconds}s...", "Warning");

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                // Exponential backoff
                delaySeconds = (int)(delaySeconds * _config.BackoffMultiplier);
            }
            catch (Exception ex) when (IsRateLimitException(ex))
            {
                retryCount++;

                if (retryCount > _config.MaxRetries)
                {
                    System.Diagnostics.Trace.WriteLine($"[{operationName}] Failed after {_config.MaxRetries} retries due to rate limiting.", "Error");
                    throw;
                }

                System.Diagnostics.Trace.WriteLine($"[{operationName}] Rate limit hit. Retry {retryCount}/{_config.MaxRetries} - waiting {delaySeconds}s...", "Warning");

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                // Exponential backoff
                delaySeconds = (int)(delaySeconds * _config.BackoffMultiplier);
            }
        }
    }

    private static bool IsRateLimitHttpException(HttpRequestException ex)
    {
        return ex.StatusCode == HttpStatusCode.TooManyRequests;
    }

    private static bool IsRateLimitException(Exception ex)
    {
        // Check for common rate limit exception patterns
        var message = ex.Message.ToLower();
        return message.Contains("429") || 
               message.Contains("rate limit") || 
               message.Contains("too many requests") ||
               (ex.InnerException != null && IsRateLimitException(ex.InnerException));
    }
}
