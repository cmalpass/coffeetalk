using System.Text.Json;
using CoffeeTalk.Gui.Services;

namespace CoffeeTalk.Tests;

public sealed class ConversationExportServiceTests
{
    [Fact]
    public void RenderMarkdown_DisablesRawHtmlAndEventHandlers()
    {
        var rendered = ConversationExportService.RenderMarkdown(
            "<script>alert('x')</script><div onclick=\"alert('x')\">message</div>");

        Assert.DoesNotContain("<script", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" onclick=\"", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", rendered);
    }

    [Fact]
    public void RenderMarkdown_BlocksUnsafeLinkSchemes()
    {
        var rendered = ConversationExportService.RenderMarkdown(
            "[unsafe](javascript:alert(1)) [safe](https://example.com)");

        Assert.DoesNotContain("javascript:", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"https://example.com\"", rendered);
    }

    [Fact]
    public void GenerateHtml_EscapesTopicParticipantsAndMessageMetadata()
    {
        var message = new ChatMessage
        {
            Sender = "<img src=x onerror=alert(1)>",
            Content = "safe",
            Timestamp = new DateTime(2026, 1, 2, 3, 4, 0)
        };

        var html = ConversationExportService.GenerateHtml(
            "<script>alert(1)</script>",
            new DateTime(2026, 1, 2, 3, 4, 0),
            new[] { "<b>participant</b>" },
            new[] { message });

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("&lt;b&gt;participant&lt;/b&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
    }

    [Fact]
    public void GenerateMarkdown_IncludesMetadataAndEveryMessage()
    {
        var messages = new[]
        {
            new ChatMessage { Sender = "Agent A", Content = "First response" },
            new ChatMessage { Sender = "System", Content = "A divider", IsDivider = true },
            new ChatMessage { Sender = "Agent B", Content = "Second response" }
        };

        var markdown = ConversationExportService.GenerateMarkdown(
            "Project topic",
            new DateTime(2026, 1, 2, 3, 4, 0),
            new[] { "Agent A", "Agent B" },
            messages);

        Assert.Contains("# Project topic", markdown);
        Assert.Contains("Started: 2026-01-02 03:04", markdown);
        Assert.Contains("Participants: Agent A, Agent B", markdown);
        Assert.Contains("First response", markdown);
        Assert.Contains("A divider", markdown);
        Assert.Contains("Second response", markdown);
    }

    [Fact]
    public void GenerateMarkdown_PreservesSpeakerOrderingForSystemAndDividerMessages()
    {
        var markdown = ConversationExportService.GenerateMarkdown(
            "Topic",
            null,
            new[] { "A", "B" },
            new[]
            {
                new ChatMessage { Sender = "System", Content = "started", IsSystem = true },
                new ChatMessage { Sender = "A", Content = "first" },
                new ChatMessage { Sender = "System", Content = "round", IsDivider = true },
                new ChatMessage { Sender = "B", Content = "second" }
            });

        Assert.True(markdown.IndexOf("started", StringComparison.Ordinal) < markdown.IndexOf("first", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("---", StringComparison.Ordinal) > markdown.IndexOf("first", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("---", StringComparison.Ordinal) < markdown.IndexOf("second", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("first", StringComparison.Ordinal) < markdown.IndexOf("second", StringComparison.Ordinal));
        Assert.Contains("**System**", markdown);
        Assert.Contains("---", markdown);
    }

    [Fact]
    public void GenerateJson_ProducesValidJsonWithAllMessages()
    {
        var messages = new[]
        {
            new ChatMessage { Sender = "Agent", Content = "quoted \"value\"\nnext" }
        };

        var json = ConversationExportService.GenerateJson(
            "Topic",
            DateTime.UtcNow,
            new[] { "Agent" },
            messages);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("Topic", document.RootElement.GetProperty("topic").GetString());
        Assert.Single(document.RootElement.GetProperty("messages").EnumerateArray());
        Assert.Equal("quoted \"value\"\nnext",
            document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }
}
