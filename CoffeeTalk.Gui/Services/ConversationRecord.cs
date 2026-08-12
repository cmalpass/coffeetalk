namespace CoffeeTalk.Gui.Services;

public sealed class ConversationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Topic { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Completed";
    public int MessageCount { get; set; }
    public List<string> Personas { get; set; } = new();
    public List<ChatMessage> Messages { get; set; } = new();
    public string DocumentContent { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}
