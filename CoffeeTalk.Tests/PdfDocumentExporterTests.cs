using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Services;
using System.Text;

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
}
