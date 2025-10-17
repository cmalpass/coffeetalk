namespace CoffeeTalk.Models;

public class RetryConfig
{
    public int InitialDelaySeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 5;
    public double BackoffMultiplier { get; set; } = 2.0;
}
