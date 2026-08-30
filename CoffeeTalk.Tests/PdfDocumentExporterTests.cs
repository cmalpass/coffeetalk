using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Services;
using PdfSharp.Drawing;
using System.Text;
using System.Threading.Tasks;

namespace CoffeeTalk.Tests;

public sealed class PdfDocumentExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesPdfForCollaborativeMarkdown()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-pdf-tests", Guid.NewGuid().ToString("N"));
        var resolver = new ApplicationDataPathResolver(root);
        var path = resolver.ResolveExportPath("final.pdf", "conversation.pdf");

        try
        {
            IPdfDocumentExporter exporter = new PdfDocumentExporter();
            await exporter.ExportAsync("# Final document\n\nA collaborative result.", path);

            Assert.True(File.Exists(path));
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveExportPath_RejectsTraversalForPdfOutput()
    {
        var resolver = new ApplicationDataPathResolver(Path.Combine(Path.GetTempPath(), "coffeetalk-pdf-tests"));

        Assert.Throws<UnauthorizedAccessException>(
            () => resolver.ResolveExportPath("../final.pdf", "conversation.pdf"));
    }

    [Fact]
    public void FindFontFile_UsesFirstExistingCandidate()
    {
        using var dir = new TempDir("coffeetalk-font-candidates");
        var candidate = Path.Combine(dir.Path, "MyCustomFont.ttf");
        File.WriteAllBytes(candidate, [0x00, 0x01, 0x00, 0x00]);

        var found = SystemFontDiscovery.FindFontFile(
            new[] { Path.Combine(dir.Path, "missing.ttf"), candidate });

        Assert.Equal(candidate, found);
    }

    [Fact]
    public void FindFontFile_ReturnsNull_WhenNoCandidateExists()
    {
        using var dir = new TempDir("coffeetalk-font-missing");

        var found = SystemFontDiscovery.FindFontFile(new[] { Path.Combine(dir.Path, "nope.ttf") });

        Assert.Null(found);
    }

    [Fact]
    public void FindFontFileOrDefault_Throws_WhenNoFontAvailable()
    {
        using var dir = new TempDir("coffeetalk-font-none");

        var ex = Assert.Throws<InvalidOperationException>(
            () => SystemFontDiscovery.FindFontFileOrDefault(new[] { Path.Combine(dir.Path, "nope.ttf") }));

        Assert.Contains("font", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolver_GetFont_LoadsBytesOnlyOnce_AndCaches()
    {
        var calls = 0;
        var resolver = new SystemFontResolver(
            "/tmp/fake-font.ttf",
            _ => { calls++; return new byte[] { 0x00, 0x01, 0x00, 0x00 }; });

        var first = resolver.GetFont(SystemFontResolver.FaceName);
        var second = resolver.GetFont(SystemFontResolver.FaceName);

        Assert.Same(first, second); // cached instance, no repeat read
        Assert.Equal(1, calls);     // underlying file only read once
        Assert.Equal(new byte[] { 0x00, 0x01, 0x00, 0x00 }, first);
    }

    [Fact]
    public void Resolver_ResolveTypeface_HonorsBoldAndItalic()
    {
        using var dir = new TempDir("coffeetalk-font-resolver");
        var resolver = new SystemFontResolver(Path.Combine(dir.Path, "font.ttf"));

        var regular = resolver.ResolveTypeface("Arial", isBold: false, isItalic: false);
        var bold = resolver.ResolveTypeface("Arial", isBold: true, isItalic: false);
        var italic = resolver.ResolveTypeface("Arial", isBold: false, isItalic: true);
        var boldItalic = resolver.ResolveTypeface("Arial", isBold: true, isItalic: true);

        Assert.All(new[] { regular, bold, italic, boldItalic }, info => Assert.Equal(SystemFontResolver.FaceName, info.FaceName));
        Assert.False(regular.MustSimulateBold);
        Assert.True(bold.MustSimulateBold);
        Assert.True(italic.MustSimulateItalic);
        Assert.True(boldItalic.MustSimulateBold);
        Assert.True(boldItalic.MustSimulateItalic);
        Assert.False(regular.MustSimulateItalic);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir(string prefix)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                prefix,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
