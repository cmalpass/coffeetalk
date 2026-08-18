namespace CoffeeTalk.Services;

/// <summary>
/// Defines the stateless context contract used by persona and orchestrator prompts.
/// The application owns the conversation state; provider-managed threads are not used.
/// </summary>
public static class AgentContextPolicy
{
    public const int MaxPromptCharacters = 24_000;
    public const int MaxDocumentCharacters = 12_000;
    public const int MaxHistoryCharacters = 6_000;
    public const int MaxHistoryEntryCharacters = 2_000;
    public const int MaxCurrentMessageCharacters = 4_000;

    public static string Limit(string value, int maximumCharacters)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCharacters, 1);
        if (value.Length <= maximumCharacters)
            return value;

        const string marker = "\n\n[… context truncated …]\n\n";
        if (maximumCharacters <= marker.Length)
            return value[..maximumCharacters];

        var available = maximumCharacters - marker.Length;
        var prefixLength = (available + 1) / 2;
        var suffixLength = available / 2;
        return value[..prefixLength] + marker + value[^suffixLength..];
    }

    public static string LimitDocument(string document) =>
        Limit(document, MaxDocumentCharacters);

    public static string LimitHistoryEntry(string entry) =>
        Limit(entry, MaxHistoryEntryCharacters);

    public static string LimitHistory(IEnumerable<string> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        return Limit(string.Join("\n", history.TakeLast(5).Select(LimitHistoryEntry)), MaxHistoryCharacters);
    }

    public static string LimitCurrentMessage(string message) =>
        Limit(message, MaxCurrentMessageCharacters);
}
