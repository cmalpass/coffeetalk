using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public static class ConversationMetricsCalculator
{
    private const double ApproxCharsPerToken = 4.0;

    public static ConversationMetrics Calculate(ConversationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var messages = state.Messages.Where(message => !message.IsSystem && !message.IsDivider).ToList();
        var metrics = new ConversationMetrics
        {
            Duration = state.CompletedAt.HasValue
                ? state.CompletedAt.Value - state.StartedAt
                : TimeSpan.Zero,
            MessageCount = messages.Count,
            DocumentWordCount = CountWords(state.DocumentContent),
            DocumentHeadingCount = state.DocumentContent
                .Split('\n')
                .Count(line => line.TrimStart().StartsWith('#'))
        };

        foreach (var message in messages)
        {
            var words = CountWords(message.Content);
            metrics.WordCount += words;
            metrics.EstimatedTokenCount += EstimateTokens(message.Content);
            metrics.MessagesByParticipant[message.Sender] =
                metrics.MessagesByParticipant.GetValueOrDefault(message.Sender) + 1;
            metrics.WordsByParticipant[message.Sender] =
                metrics.WordsByParticipant.GetValueOrDefault(message.Sender) + words;
        }

        if (metrics.Duration < TimeSpan.Zero)
            metrics.Duration = TimeSpan.Zero;

        return metrics;
    }

    private static int CountWords(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (int)Math.Ceiling(text.Length / ApproxCharsPerToken));
}

public sealed class ConversationAnalyticsSummary
{
    public int ConversationCount { get; init; }
    public int MessageCount { get; init; }
    public int WordCount { get; init; }
    public int EstimatedTokenCount { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public IReadOnlyDictionary<string, int> MessagesByParticipant { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
}

public static class ConversationMetricsAggregator
{
    public static ConversationAnalyticsSummary Summarize(IEnumerable<ConversationState> states)
    {
        var items = states.ToList();
        var participantCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var state in items)
        {
            foreach (var pair in state.Metrics.MessagesByParticipant)
                participantCounts[pair.Key] = participantCounts.GetValueOrDefault(pair.Key) + pair.Value;
        }

        return new ConversationAnalyticsSummary
        {
            ConversationCount = items.Count,
            MessageCount = items.Sum(state => state.Metrics.MessageCount),
            WordCount = items.Sum(state => state.Metrics.WordCount),
            EstimatedTokenCount = items.Sum(state => state.Metrics.EstimatedTokenCount),
            AverageDuration = items.Count == 0
                ? TimeSpan.Zero
                : TimeSpan.FromTicks(items.Sum(state => state.Metrics.Duration.Ticks) / items.Count),
            MessagesByParticipant = participantCounts
        };
    }
}
