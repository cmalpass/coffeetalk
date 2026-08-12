namespace CoffeeTalk.Gui.Services;

public sealed class ConversationRecord
{
    public string Topic { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Completed";
    public int MessageCount { get; set; }
    public List<string> Personas { get; set; } = new();
    public List<ChatMessage> Messages { get; set; } = new();
}
