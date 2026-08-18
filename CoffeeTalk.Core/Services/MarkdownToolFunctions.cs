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
        using var arguments = JsonDocument.Parse(argumentsJson);
        return toolName switch
        {
            "SetTitle" => SetTitle(arguments.RootElement.GetProperty("title").GetString() ?? string.Empty),
            "AddHeading" => AddHeading(
                arguments.RootElement.GetProperty("text").GetString() ?? string.Empty,
                arguments.RootElement.TryGetProperty("level", out var level) ? level.GetInt32() : 2),
            "AppendParagraph" => AppendParagraph(arguments.RootElement.GetProperty("text").GetString() ?? string.Empty),
            "InsertAfterHeading" => InsertAfterHeading(
                arguments.RootElement.GetProperty("headingText").GetString() ?? string.Empty,
                arguments.RootElement.GetProperty("content").GetString() ?? string.Empty),
            "ReplaceSection" => ReplaceSection(
                arguments.RootElement.GetProperty("headingText").GetString() ?? string.Empty,
                arguments.RootElement.GetProperty("content").GetString() ?? string.Empty),
            "ListHeadings" => ListHeadings(),
            _ => throw new ArgumentException($"Unsupported markdown tool: {toolName}", nameof(toolName))
        };
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
