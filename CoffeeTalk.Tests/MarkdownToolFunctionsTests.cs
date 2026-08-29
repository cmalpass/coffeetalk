using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public class MarkdownToolFunctionsTests
{
    private static MarkdownToolFunctions CreateTool()
        => new(new CollaborativeMarkdownDocument());

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("{\"title\": }")]
    public void ExecuteJsonTool_MalformedJson_ReturnsRecoverableError(string malformedJson)
    {
        var tool = CreateTool();

        var result = tool.ExecuteJsonTool("SetTitle", malformedJson);

        Assert.StartsWith("Error: invalid JSON arguments", result);
    }

    [Fact]
    public void ExecuteJsonTool_NonObjectJson_ReturnsError()
    {
        var tool = CreateTool();

        var result = tool.ExecuteJsonTool("SetTitle", "[1, 2, 3]");

        Assert.StartsWith("Error: invalid JSON arguments", result);
    }

    [Theory]
    [InlineData("SetTitle", "{}")]
    [InlineData("AddHeading", "{}")]
    [InlineData("AppendParagraph", "{}")]
    [InlineData("InsertAfterHeading", "{}")]
    [InlineData("ReplaceSection", "{}")]
    [InlineData("InsertAfterHeading", "{\"headingText\":\"H\"}")]
    [InlineData("ReplaceSection", "{\"content\":\"C\"}")]
    public void ExecuteJsonTool_MissingRequiredKey_ReturnsClearError(string toolName, string argumentsJson)
    {
        var tool = CreateTool();

        var result = tool.ExecuteJsonTool(toolName, argumentsJson);

        Assert.StartsWith("Error: missing required argument", result);
    }

    [Fact]
    public void ExecuteJsonTool_SetTitle_Valid()
    {
        var tool = CreateTool();

        var result = tool.ExecuteJsonTool("SetTitle", "{\"title\": \"My Title\"}");

        Assert.Contains("# My Title", result);
    }

    [Fact]
    public void ExecuteJsonTool_AddHeading_DefaultLevel()
    {
        var tool = CreateTool();

        var result = tool.ExecuteJsonTool("AddHeading", "{\"text\": \"Section\"}");

        Assert.Contains("## Section", result);
    }

    [Fact]
    public void ExecuteJsonTool_AddHeading_ExplicitLevel()
    {
        var tool = CreateTool();

        var result = tool.ExecuteJsonTool("AddHeading", "{\"text\": \"Section\", \"level\": 3}");

        Assert.Contains("### Section", result);
    }

    [Fact]
    public void ExecuteJsonTool_AppendParagraph_Valid()
    {
        var tool = CreateTool();

        var result = tool.ExecuteJsonTool("AppendParagraph", "{\"text\": \"Hello\"}");

        Assert.Contains("Hello", result);
    }

    [Fact]
    public void ExecuteJsonTool_InsertAfterHeading_Valid()
    {
        var tool = CreateTool();
        tool.ExecuteJsonTool("AddHeading", "{\"text\": \"Section\"}");

        var result = tool.ExecuteJsonTool(
            "InsertAfterHeading",
            "{\"headingText\": \"Section\", \"content\": \"New content\"}");

        Assert.Contains("New content", result);
    }

    [Fact]
    public void ExecuteJsonTool_ReplaceSection_Valid()
    {
        var tool = CreateTool();
        tool.ExecuteJsonTool("AddHeading", "{\"text\": \"Section\"}");

        var result = tool.ExecuteJsonTool(
            "ReplaceSection",
            "{\"headingText\": \"Section\", \"content\": \"Replacement\"}");

        Assert.Contains("Replacement", result);
    }

    [Fact]
    public void ExecuteJsonTool_ListHeadings_Valid()
    {
        var tool = CreateTool();
        tool.ExecuteJsonTool("AddHeading", "{\"text\": \"Alpha\"}");
        tool.ExecuteJsonTool("AddHeading", "{\"text\": \"Beta\", \"level\": 3}");

        var result = tool.ExecuteJsonTool("ListHeadings", "{}");

        Assert.Contains("Alpha", result);
        Assert.Contains("Beta", result);
    }

    [Fact]
    public void ExecuteJsonTool_UnsupportedTool_ReturnsError()
    {
        var tool = CreateTool();

        var result = tool.ExecuteJsonTool("NoSuchTool", "{}");

        Assert.StartsWith("Error: unsupported markdown tool", result);
    }
}
