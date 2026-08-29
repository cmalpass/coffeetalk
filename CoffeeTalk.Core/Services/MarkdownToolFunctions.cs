using Microsoft.Extensions.AI;
using CoffeeTalk.Core.Interfaces;
using System.ComponentModel;
using System.Text.Json;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

/// <summary>
/// Provides markdown document collaboration functions for use with Microsoft Agent Framework
/// </summary>
public class MarkdownToolFunctions
{
    private readonly CollaborativeMarkdownDocument _doc;
    private readonly IOperationalEventSink _eventSink;
    public ToolsConfig Configuration { get; }

    public MarkdownToolFunctions(
        CollaborativeMarkdownDocument doc,
        ToolsConfig? configuration = null,
        IOperationalEventSink? eventSink = null)
    {
        _doc = doc;
        Configuration = configuration ?? new ToolsConfig();
        _eventSink = eventSink ?? NullOperationalEventSink.Instance;
    }

    /// <summary>
    /// Creates an array of AIFunction tools for markdown document collaboration
    /// </summary>
    public AIFunction[] CreateTools()
    {
        var tools = new List<AIFunction>
        {
            AIFunctionFactory.Create(SetTitle),
            AIFunctionFactory.Create(AddHeading),
            AIFunctionFactory.Create(AppendParagraph),
            AIFunctionFactory.Create(InsertAfterHeading),
            AIFunctionFactory.Create(ReplaceSection),
            AIFunctionFactory.Create(ListHeadings),
            AIFunctionFactory.Create(SaveToFileAsync)
        };

        if (Configuration.EnableFallbackJsonTools)
        {
            tools.Add(AIFunctionFactory.Create(ExecuteJsonTool));
        }

        return tools.ToArray();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "This remains an instance method to preserve the public tool-helper API.")]
    public bool VerifyTools(AIFunction[] tools)
    {
        return tools.Length >= 7;
    }

    [Description("Set the title (H1) of the shared markdown document")]
    public string SetTitle([Description("The title text to set as H1")] string title)
        => ExecuteTool("SetTitle", title, () => _doc.SetTitle(title));

    [Description("Add a heading to the shared markdown document")]
    public string AddHeading(
        [Description("Heading text")] string text,
        [Description("Heading level 1-6; default 2")] int level = 2)
        => ExecuteTool("AddHeading", $"{text}\nlevel={level}", () => _doc.AddHeading(text, level));

    [Description("Append a paragraph to the shared markdown document")]
    public string AppendParagraph([Description("Paragraph text")] string text)
        => ExecuteTool("AppendParagraph", text, () => _doc.AppendParagraph(text));

    [Description("Insert content after a specific heading; creates the heading if missing")]
    public string InsertAfterHeading(
        [Description("Heading text to insert after")] string headingText,
        [Description("Markdown content to insert")] string content)
        => ExecuteTool("InsertAfterHeading", $"{headingText}\n{content}", () => _doc.InsertAfterHeading(headingText, content));

    [Description("Replace the content of a section under a heading with new, concise content. Creates the section if not present.")]
    public string ReplaceSection(
        [Description("The exact heading text whose section content should be replaced")] string headingText,
        [Description("The new concise markdown content for the section")] string content)
        => ExecuteTool("ReplaceSection", $"{headingText}\n{content}", () => _doc.ReplaceSection(headingText, content));

    [Description("List all headings currently in the document")]
    public string ListHeadings()
        => ExecuteTool("ListHeadings", null, () => _doc.ListHeadings());

    [Description("Save the shared markdown document to disk and return the full file path")]
    public Task<string> SaveToFileAsync([Description("Output path relative to the CoffeeTalk exports directory; defaults to conversation.md")] string? path = null)
        => ExecuteToolAsync("SaveToFile", path, () => _doc.SaveToFileAsync(path ?? "conversation.md"));

    [Description("Fallback tool dispatcher for providers that return tool calls as JSON")]
    public string ExecuteJsonTool(
        [Description("Tool name, such as SetTitle or AppendParagraph")] string toolName,
        [Description("JSON object containing the tool arguments")] string argumentsJson)
    {
        // Provider tool arguments are untrusted model output and may occasionally be malformed or
        // missing required keys. Rather than throwing (which can abort the tool call / turn), we
        // return a recoverable error string the model can read and react to, matching the
        // fallback-tool pattern used by other markdown tools.
        try
        {
            using var arguments = JsonDocument.Parse(argumentsJson);
            var root = arguments.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return $"Error: invalid JSON arguments for tool '{toolName}': expected a JSON object";
            }

            return toolName switch
            {
                "SetTitle" => RequireString(root, "title", out var title)
                    ? SetTitle(title)
                    : $"Error: missing required argument 'title' for tool '{toolName}'",
                "AddHeading" => RequireString(root, "text", out var text)
                    ? AddHeading(text, ReadOptionalLevel(root))
                    : $"Error: missing required argument 'text' for tool '{toolName}'",
                "AppendParagraph" => RequireString(root, "text", out var paragraph)
                    ? AppendParagraph(paragraph)
                    : $"Error: missing required argument 'text' for tool '{toolName}'",
                "InsertAfterHeading" => RequireTwoStrings(root, "headingText", "content", out var insertHeading, out var insertContent)
                    ? InsertAfterHeading(insertHeading, insertContent)
                    : $"Error: missing required arguments 'headingText' and 'content' for tool '{toolName}'",
                "ReplaceSection" => RequireTwoStrings(root, "headingText", "content", out var sectionHeading, out var sectionContent)
                    ? ReplaceSection(sectionHeading, sectionContent)
                    : $"Error: missing required arguments 'headingText' and 'content' for tool '{toolName}'",
                "ListHeadings" => ListHeadings(),
                _ => $"Error: unsupported markdown tool: {toolName}"
            };
        }
        catch (JsonException ex)
        {
            return $"Error: invalid JSON arguments for tool '{toolName}': {ex.Message}";
        }
    }

    private static bool RequireString(JsonElement root, string name, out string value)
    {
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool RequireTwoStrings(JsonElement root, string firstName, string secondName, out string first, out string second)
    {
        first = string.Empty;
        second = string.Empty;
        return RequireString(root, firstName, out first) && RequireString(root, secondName, out second);
    }

    private static int ReadOptionalLevel(JsonElement root)
    {
        return root.TryGetProperty("level", out var level) && level.ValueKind == JsonValueKind.Number
            ? level.GetInt32()
            : 2;
    }

    private string ExecuteTool(string name, string? arguments, Action action)
    {
        var telemetry = new ToolTelemetry(_eventSink, $"Tool: {name}", arguments);
        try
        {
            action();
            var result = _doc.GetContent();
            telemetry.Complete(result);
            return result;
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);
            throw;
        }
    }

    private async Task<string> ExecuteToolAsync(
        string name,
        string? arguments,
        Func<Task<string>> action)
    {
        var telemetry = new ToolTelemetry(_eventSink, $"Tool: {name}", arguments);
        try
        {
            var result = await action();
            telemetry.Complete(result);
            return result;
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);
            throw;
        }
    }
}
