using CoffeeTalk.Core.Interfaces;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System.Text;

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
        var headingFont = new XFont("Arial", 16, XFontStyleEx.Bold);
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var lineIndex = 0;
        var pendingLines = new Queue<PendingLine>();
        while (lineIndex < lines.Length || pendingLines.Count > 0)
        {
            var page = document.AddPage();
            using var graphics = XGraphics.FromPdfPage(page);
            var y = 40d;
            while (lineIndex < lines.Length || pendingLines.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (pendingLines.Count == 0)
                {
                    var line = lines[lineIndex++];
                    var headingLength = GetHeadingLength(line);
                    var isHeading = headingLength > 0;
                    var text = isHeading ? line[(headingLength + 1)..] : line;
                    var fontForLine = isHeading ? headingFont : font;
                    var lineHeight = isHeading ? 24d : 16d;
                    foreach (var wrappedText in WrapText(graphics, text, fontForLine, page.Width.Point - 80, cancellationToken))
                        pendingLines.Enqueue(new PendingLine(wrappedText, fontForLine, lineHeight));
                }

                var pendingLine = pendingLines.Peek();
                var lineHeightForPage = pendingLine.LineHeight;
                if (y + lineHeightForPage > page.Height.Point - 40)
                {
                    break;
                }

                pendingLines.Dequeue();
                DrawLine(graphics, pendingLine.Text, pendingLine.Font, ref y, lineHeightForPage, page.Width.Point);
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

    private static IEnumerable<string> WrapText(
        XGraphics graphics,
        string text,
        XFont font,
        double maxWidth,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        var currentLine = string.Empty;
        foreach (var rune in text.EnumerateRunes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = rune.ToString();
            var candidate = currentLine + character;
            if (currentLine.Length == 0 || graphics.MeasureString(candidate, font).Width <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            var breakIndex = currentLine.LastIndexOfAny([' ', '\t']);
            if (breakIndex >= 0)
            {
                yield return currentLine[..(breakIndex + 1)];
                currentLine = currentLine[(breakIndex + 1)..] + character;
            }
            else
            {
                yield return currentLine;
                currentLine = character;
            }
        }

        if (currentLine.Length > 0)
            yield return currentLine;
    }

    private readonly record struct PendingLine(string Text, XFont Font, double LineHeight);
}
