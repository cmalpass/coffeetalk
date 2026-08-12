namespace CoffeeTalk.Models;

public static class ConversationStateSchema
{
    public const int CurrentVersion = 1;
}

public sealed class ConversationState
{
    public int SchemaVersion { get; set; } = ConversationStateSchema.CurrentVersion;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Topic { get; set; } = string.Empty;
    public string DocumentContent { get; set; } = string.Empty;
    public List<ConversationMessage> Messages { get; set; } = new();
    public List<ConversationParticipant> Participants { get; set; } = new();
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Status { get; set; } = "Completed";
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
    public ConversationMetrics Metrics { get; set; } = new();
}

public sealed class ConversationMessage
{
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public bool IsError { get; set; }
    public bool IsDivider { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ConversationParticipant
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ConversationMetrics
{
    public TimeSpan Duration { get; set; }
    public int MessageCount { get; set; }
    public int WordCount { get; set; }
    public int EstimatedTokenCount { get; set; }
    public int DocumentWordCount { get; set; }
    public int DocumentHeadingCount { get; set; }
    public Dictionary<string, int> MessagesByParticipant { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> WordsByParticipant { get; set; } = new(StringComparer.Ordinal);
}
