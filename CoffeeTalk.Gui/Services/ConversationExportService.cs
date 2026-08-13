using System.Net;
using System.Text;
using System.Text.Json;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text.RegularExpressions;

namespace CoffeeTalk.Gui.Services;

public static class ConversationExportService
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseGridTables()
        .Build();

    public static string RenderMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var document = Markdig.Markdown.Parse(markdown.Trim(), MarkdownPipeline);
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeLink(link.Url))
                link.Url = null;
        }

        var html = Markdig.Markdown.ToHtml(document, MarkdownPipeline);
        return Regex.Replace(
            html,
            "<pre><code class=\"language-mermaid\">(?<diagram>.*?)</code></pre>",
            "<div class=\"mermaid\">${diagram}</div>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    public static string GenerateMarkdown(
        string? topic,
        DateTime? startedAt,
        IReadOnlyCollection<string> participants,
        IReadOnlyList<ChatMessage> messages)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(EscapeMarkdown(topic ?? "Conversation"));
        builder.Append("Started: ").AppendLine(startedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown");
        builder.Append("Participants: ").AppendLine(string.Join(", ", participants.Select(EscapeMarkdown)));
        builder.AppendLine();

        foreach (var message in messages)
        {
            if (message.IsDivider)
            {
                builder.AppendLine("---");
                if (!string.IsNullOrWhiteSpace(message.Content))
                    builder.AppendLine(message.Content);
                builder.AppendLine();
                continue;
            }

            builder.Append("**").Append(EscapeMarkdown(message.Sender)).Append("** ");
            builder.Append("_(").Append(message.Timestamp.ToString("HH:mm")).AppendLine(")_");
            builder.AppendLine();
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    public static string GenerateHtml(
        string? topic,
        DateTime? startedAt,
        IReadOnlyCollection<string> participants,
        IReadOnlyList<ChatMessage> messages)
    {
        var title = EscapeHtml(topic ?? "Conversation");
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>")
            .AppendLine("<html>")
            .AppendLine("<head>")
            .AppendLine("    <meta charset=\"utf-8\">")
            .Append("    <title>").Append(title).AppendLine("</title>")
            .AppendLine("    <style>")
            .AppendLine("        body { font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; }")
            .AppendLine("        .message { margin: 10px 0; padding: 10px; border-left: 3px solid #ccc; }")
            .AppendLine("        .sender { font-weight: bold; color: #666; }")
            .AppendLine("        .timestamp { color: #999; font-size: 0.8em; }")
            .AppendLine("        .system { background: #f5f5f5; padding: 5px; border-radius: 3px; }")
            .AppendLine("        .error { background: #ffebee; padding: 5px; border-radius: 3px; }")
            .AppendLine("    </style>")
            .AppendLine("</head>")
            .AppendLine("<body>")
            .Append("    <h1>").Append(title).AppendLine("</h1>")
            .Append("    <p>Started: ")
            .Append(EscapeHtml(startedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown"))
            .AppendLine("</p>")
            .Append("    <p>Participants: ")
            .Append(EscapeHtml(string.Join(", ", participants)))
            .AppendLine("</p>")
            .AppendLine("    <hr/>");

        foreach (var message in messages)
        {
            if (message.IsDivider)
            {
                builder.AppendLine("    <hr/>");
            }
            else if (message.IsSystem)
            {
                builder.Append("    <div class=\"system\">")
                    .Append(EscapeHtml(message.Content))
                    .AppendLine("</div>");
            }
            else if (message.IsError)
            {
                builder.Append("    <div class=\"error\">")
                    .Append(EscapeHtml(message.Content))
                    .AppendLine("</div>");
            }
            else
            {
                builder.AppendLine("    <div class=\"message\">")
                    .Append("        <span class=\"sender\">")
                    .Append(EscapeHtml(message.Sender))
                    .Append("</span> <span class=\"timestamp\">(")
                    .Append(message.Timestamp.ToString("HH:mm"))
                    .AppendLine(")</span>")
                    .Append("        <div>")
                    .Append(RenderMarkdown(message.Content))
                    .AppendLine("</div>")
                    .AppendLine("    </div>");
            }
        }

        return builder.AppendLine("</body>").AppendLine("</html>").ToString();
    }

    public static string GenerateJson(
        string? topic,
        DateTime? startedAt,
        IReadOnlyCollection<string> participants,
        IReadOnlyList<ChatMessage> messages)
    {
        return JsonSerializer.Serialize(new
        {
            topic = topic ?? string.Empty,
            startedAt,
            participants,
            messages
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
    }

    private static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);

    private static bool IsSafeLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("//", StringComparison.Ordinal))
            return false;

        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri) || !uri.IsAbsoluteUri)
            return true;

        return uri.Scheme is "http" or "https" or "mailto";
    }

    private static string EscapeMarkdown(string text) =>
        text.Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace("#", "\\#")
            .Replace("|", "\\|");
}
