using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class ApplicationDataPathResolverTests
{
    [Fact]
    public void ResolveExportPath_UsesExportDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
        var resolver = new ApplicationDataPathResolver(root);

        var path = resolver.ResolveExportPath("nested/conversation.md", "conversation.md");

        Assert.Equal(Path.Combine(root, "exports", "nested", "conversation.md"), path);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/tmp/outside.txt")]
    public void ResolveDataPath_RejectsEscapes(string path)
    {
        var resolver = new ApplicationDataPathResolver(Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N")));

        Assert.Throws<UnauthorizedAccessException>(() => resolver.ResolveDataPath(path, "data.json"));
    }
}
