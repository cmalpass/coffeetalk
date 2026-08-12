using Xunit;
using CoffeeTalk.Services;
using System.IO;

namespace CoffeeTalk.Tests;

public class CollaborativeMarkdownDocumentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InsertAfterHeading_InvalidContentDoesNotMutateDocument(string? content)
    {
        var document = new CollaborativeMarkdownDocument();
        document.AddHeading("Position");
        document.AppendParagraph("Keep this content.");
        var before = document.Snapshot();

        document.InsertAfterHeading("Position", content);

        Assert.Equal(before, document.Snapshot());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReplaceSection_InvalidContentDoesNotMutateDocument(string? content)
    {
        var document = new CollaborativeMarkdownDocument();
        document.AddHeading("Position");
        document.AppendParagraph("Keep this content.");
        var before = document.Snapshot();

        document.ReplaceSection("Position", content);

        Assert.Equal(before, document.Snapshot());
    }

    [Fact]
    public void SetTitle_ShouldAddTitle_WhenDocumentIsEmpty()
    {
        var doc = new CollaborativeMarkdownDocument();
        doc.SetTitle("My Document");
        var content = doc.GetContent();
        Assert.StartsWith("# My Document", content);
    }

    [Fact]
    public void SetTitle_ShouldReplaceTitle_WhenTitleExists()
    {
        var doc = new CollaborativeMarkdownDocument();
        doc.SetTitle("Old Title");
        doc.SetTitle("New Title");
        var content = doc.GetContent();
        Assert.Contains("# New Title", content);
        Assert.DoesNotContain("# Old Title", content);
    }

    [Fact]
    public void AddHeading_ShouldAppendHeading()
    {
        var doc = new CollaborativeMarkdownDocument();
        doc.AddHeading("Section 1");
        Assert.Contains("## Section 1", doc.GetContent());
    }

    [Fact]
    public void ReplaceSection_ShouldCreateSection_IfMissing()
    {
        var doc = new CollaborativeMarkdownDocument();
        doc.ReplaceSection("Overview", "This is the overview.");

        var content = doc.GetContent();
        Assert.Contains("## Overview", content);
        Assert.Contains("This is the overview.", content);
    }

    [Fact]
    public void ReplaceSection_ShouldReplaceContent_IfExists()
    {
        var doc = new CollaborativeMarkdownDocument();
        doc.ReplaceSection("Details", "Old content.");
        doc.ReplaceSection("Details", "New content.");

        var content = doc.GetContent();
        Assert.Contains("New content.", content);
        Assert.DoesNotContain("Old content.", content);
        // Should verify it didn't duplicate the header
        var count = System.Text.RegularExpressions.Regex.Matches(content, "## Details").Count;
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InsertAfterHeading_ShouldPreserveContent_WhenInsertedContentIsBlank(string? insertedContent)
    {
        var doc = new CollaborativeMarkdownDocument();
        doc.Restore("## Details\n\nExisting content.\n\n");
        var before = doc.GetContent();

        doc.InsertAfterHeading("Details", insertedContent);

        Assert.Equal(before, doc.GetContent());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReplaceSection_ShouldPreserveContent_WhenReplacementContentIsBlank(string? replacementContent)
    {
        var doc = new CollaborativeMarkdownDocument();
        doc.Restore("## Details\n\nExisting content.\n\n## Next\n\nNext content.\n\n");
        var before = doc.GetContent();

        doc.ReplaceSection("Details", replacementContent);

        Assert.Equal(before, doc.GetContent());
    }

    [Fact]
    public void Restore_ShouldRevertContent()
    {
        var doc = new CollaborativeMarkdownDocument();
        doc.AppendParagraph("Initial state");
        var snapshot = doc.Snapshot();

        doc.AppendParagraph("Mistake");
        doc.Restore(snapshot);

        Assert.DoesNotContain("Mistake", doc.GetContent());
        Assert.Contains("Initial state", doc.GetContent());
    }
}
