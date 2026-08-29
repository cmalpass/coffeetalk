namespace CoffeeTalk.Models;

public class RetryConfig
{
    public int InitialDelaySeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 5;
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Maximum backoff delay, in seconds, applied per retry. If zero or negative, the
    /// backoff is not explicitly capped (still overflow-safe). Defaults to 10 minutes.
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 600;
}
