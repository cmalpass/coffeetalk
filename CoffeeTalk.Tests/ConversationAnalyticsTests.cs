using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class ConversationAnalyticsTests
{
    [Fact]
    public void CalculateExcludesSystemAndDividerMessagesAndTracksParticipants()
    {
        var state = new ConversationState
        {
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            CompletedAt = DateTimeOffset.UtcNow,
            DocumentContent = "# Summary\nA recommendation",
            Messages =
            [
                new() { Sender = "System", Content = "hidden", IsSystem = true },
                new() { Sender = "Alice", Content = "One two." },
                new() { Sender = "Bob", Content = "Three", IsDivider = true },
                new() { Sender = "Alice", Content = "Four five." }
            ]
        };

        var metrics = ConversationMetricsCalculator.Calculate(state);

        Assert.Equal(2, metrics.MessageCount);
        Assert.Equal(4, metrics.WordCount);
        Assert.Equal(2, metrics.MessagesByParticipant["Alice"]);
        Assert.Equal(4, metrics.WordsByParticipant["Alice"]);
        Assert.Equal(5, metrics.EstimatedTokenCount);
        Assert.Equal(4, metrics.DocumentWordCount);
        Assert.Equal(1, metrics.DocumentHeadingCount);
        Assert.InRange(metrics.Duration, TimeSpan.FromMinutes(1.9), TimeSpan.FromMinutes(2.1));
    }

    [Fact]
    public void SummarizeAggregatesSavedConversationMetrics()
    {
        var first = new ConversationState
        {
            Metrics = new ConversationMetrics
            {
                MessageCount = 2, WordCount = 10, EstimatedTokenCount = 4,
                Duration = TimeSpan.FromMinutes(2),
                MessagesByParticipant = new() { ["Alice"] = 2 }
            }
        };
        var second = new ConversationState
        {
            Metrics = new ConversationMetrics
            {
                MessageCount = 1, WordCount = 5, EstimatedTokenCount = 2,
                Duration = TimeSpan.FromMinutes(4),
                MessagesByParticipant = new() { ["Bob"] = 1 }
            }
        };

        var summary = ConversationMetricsAggregator.Summarize([first, second]);

        Assert.Equal(2, summary.ConversationCount);
        Assert.Equal(3, summary.MessageCount);
        Assert.Equal(15, summary.WordCount);
        Assert.Equal(6, summary.EstimatedTokenCount);
        Assert.Equal(TimeSpan.FromMinutes(3), summary.AverageDuration);
        Assert.Equal(2, summary.MessagesByParticipant["Alice"]);
        Assert.Equal(1, summary.MessagesByParticipant["Bob"]);
    }
}
