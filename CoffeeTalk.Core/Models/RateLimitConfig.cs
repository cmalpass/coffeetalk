namespace CoffeeTalk.Models;

public class RateLimitConfig
{
    // Requests per minute cap; null disables
    public int? RequestsPerMinute { get; set; }

    // Tokens per minute cap; null disables
    public int? TokensPerMinute { get; set; }

    // Optional per-conversation caps
    public int? MaxRequestsPerConversation { get; set; }
    public int? MaxTokensPerConversation { get; set; }

    // Upper bound (in seconds) for a single per-minute wait. Caps the longest sleep so a
    // near-window-end reservation cannot stall a pipeline for a full window. Defaults to 30.
    public int? MaxPerMinuteDelaySeconds { get; set; } = 30;

    // Maximum random jitter (in milliseconds) added to a capped wait to desynchronize
    // concurrent callers. null disables jitter (default).
    public int? JitterMaxMilliseconds { get; set; }

    // Approximate token multiplier for chars->tokens if no tiktoken available
    public double ApproxCharsPerToken { get; set; } = 4.0;
}
