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

    // Approximate token multiplier for chars->tokens if no tiktoken available
    public double ApproxCharsPerToken { get; set; } = 4.0;
}
