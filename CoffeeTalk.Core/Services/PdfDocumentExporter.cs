using CoffeeTalk.Core.Interfaces;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Utils;

namespace CoffeeTalk.Services;

public sealed class PdfDocumentExporter : IPdfDocumentExporter
{
    public Task ExportAsync(string markdown, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();
        GlobalFontSettings.FontResolver ??= new SystemFontResolver();

        using var document = new PdfDocument();
        var font = new XFont("Arial", 11);
        var headingFont = new XFont("Arial", 16, XFontStyle.Bold);
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var lineIndex = 0;
        while (lineIndex < lines.Length)
        {
            var page = document.AddPage();
            using var graphics = XGraphics.FromPdfPage(page);
            var y = 40d;
            while (lineIndex < lines.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = lines[lineIndex++];
                var headingLength = GetHeadingLength(line);
                var isHeading = headingLength > 0;
                var text = isHeading ? line[(headingLength + 1)..] : line;
                var lineHeight = isHeading ? 24d : 16d;
                if (y + lineHeight > page.Height - 40)
                {
                    lineIndex--;
                    break;
                }

                DrawLine(graphics, text, isHeading ? headingFont : font, ref y, lineHeight, page.Width);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        document.Save(outputPath);
        return Task.CompletedTask;
    }

    private static int GetHeadingLength(string line)
    {
        var length = 0;
        while (length < line.Length && length < 6 && line[length] == '#')
            length++;

        return length > 0 && length < line.Length && line[length] == ' ' ? length : 0;
    }

    private sealed class SystemFontResolver : IFontResolver
    {
        private readonly string _fontPath = FindFontPath();

        public string DefaultFontName => "Arial";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new("CoffeeTalkSystemFont");

        public byte[] GetFont(string faceName) => File.ReadAllBytes(_fontPath);

        private static string FindFontPath()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf"
            };

            return candidates.FirstOrDefault(File.Exists)
                ?? throw new InvalidOperationException("No system TrueType font is available for PDF export.");
        }
    }

    private static void DrawLine(
        XGraphics graphics,
        string text,
        XFont font,
        ref double y,
        double lineHeight,
        double pageWidth)
    {
        graphics.DrawString(
            text,
            font,
            XBrushes.Black,
            new XRect(40, y, pageWidth - 80, lineHeight),
            XStringFormats.TopLeft);
        y += lineHeight;
    }
}
