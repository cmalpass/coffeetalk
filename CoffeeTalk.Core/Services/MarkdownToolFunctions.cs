using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CoffeeTalk.Services;

/// <summary>
/// Provides markdown document collaboration functions for use with Microsoft Agent Framework
/// </summary>
public class MarkdownToolFunctions
{
    private readonly CollaborativeMarkdownDocument _doc;

    public MarkdownToolFunctions(CollaborativeMarkdownDocument doc)
    {
        _doc = doc;
    }

    /// <summary>
    /// Creates an array of AIFunction tools for markdown document collaboration
    /// </summary>
    public AIFunction[] CreateTools()
    {
        return new[]
        {
            AIFunctionFactory.Create(SetTitle),
            AIFunctionFactory.Create(AddHeading),
            AIFunctionFactory.Create(AppendParagraph),
            AIFunctionFactory.Create(InsertAfterHeading),
            AIFunctionFactory.Create(ReplaceSection),
            AIFunctionFactory.Create(ListHeadings),
            AIFunctionFactory.Create(SaveToFileAsync)
        };
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
    public Task<string> SaveToFileAsync([Description("Output path; default is conversation.md in the working directory")] string? path = null)
    {
        return _doc.SaveToFileAsync(path ?? "conversation.md");
    }
}
