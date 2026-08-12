using Microsoft.Extensions.AI;
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
    public ToolsConfig Configuration { get; }

    public MarkdownToolFunctions(CollaborativeMarkdownDocument doc, ToolsConfig? configuration = null)
    {
        _doc = doc;
        Configuration = configuration ?? new ToolsConfig();
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

    public bool VerifyTools(AIFunction[] tools)
    {
        return tools.Length >= 7;
    }

    [Description("Set the title (H1) of the shared markdown document")]
    public string SetTitle([Description("The title text to set as H1")] string title)
    {
        _doc.SetTitle(title);
        return _doc.GetContent();
    }

    [Description("Add a heading to the shared markdown document")]
    public string AddHeading(
        [Description("Heading text")] string text,
        [Description("Heading level 1-6; default 2")] int level = 2)
    {
        _doc.AddHeading(text, level);
        return _doc.GetContent();
    }

    [Description("Append a paragraph to the shared markdown document")]
    public string AppendParagraph([Description("Paragraph text")] string text)
    {
        _doc.AppendParagraph(text);
        return _doc.GetContent();
    }

    [Description("Insert content after a specific heading; creates the heading if missing")]
    public string InsertAfterHeading(
        [Description("Heading text to insert after")] string headingText,
        [Description("Markdown content to insert")] string content)
    {
        _doc.InsertAfterHeading(headingText, content);
        return _doc.GetContent();
    }

    [Description("Replace the content of a section under a heading with new, concise content. Creates the section if not present.")]
    public string ReplaceSection(
        [Description("The exact heading text whose section content should be replaced")] string headingText,
        [Description("The new concise markdown content for the section")] string content)
    {
        _doc.ReplaceSection(headingText, content);
        return _doc.GetContent();
    }

    [Description("List all headings currently in the document")]
    public string ListHeadings()
    {
        return _doc.ListHeadings();
    }

    [Description("Save the shared markdown document to disk and return the full file path")]
    public Task<string> SaveToFileAsync([Description("Output path relative to the CoffeeTalk exports directory; defaults to conversation.md")] string? path = null)
    {
        return _doc.SaveToFileAsync(path ?? "conversation.md");
    }

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
}
