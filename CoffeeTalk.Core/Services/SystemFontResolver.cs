using PdfSharp.Fonts;

namespace CoffeeTalk.Services;

/// <summary>
/// Provides a single cached system font to PDFsharp and resolves requested styles
/// (bold/italic) through style simulation.
/// </summary>
/// <remarks>
/// <para>
/// Only a single resolved font file is available: the font closest to the requested
/// family that exists on the current host (see <see cref="SystemFontDiscovery"/>).
/// Because that provider returns at most one font, <c>familyName</c> is not honored as
/// separate glyph data — all families map to the same underlying face. Bold and italic
/// are honored at the typeface level via <see cref="FontResolverInfo"/> style
/// simulation, which PDFsharp renders by synthesizing the stroke/slant. This keeps the
/// resolver correct (regular, bold, italic and bold-italic all resolve distinctly)
/// without requiring distinct font files that may not exist on the host.
/// </para>
/// <para>
/// The font bytes are read from disk once and cached for the lifetime of this instance,
/// which lives for the process (see <c>GlobalFontSettings.FontResolver</c>), so repeated
/// exports and repeated <see cref="GetFont"/> calls do not re-read the file. Access is
/// synchronized so a shared resolver is safe for concurrent exports.
/// </para>
/// </remarks>
internal sealed class SystemFontResolver : IFontResolver
{
    internal const string FaceName = "CoffeeTalkSystemFont";
    internal const string DefaultFamilyName = "Arial";

    private readonly string _fontFilePath;
    private readonly Func<string, byte[]> _byteLoader;

    private readonly object _sync = new();
    private readonly Dictionary<string, byte[]> _cache;

    internal SystemFontResolver(string fontFilePath, Func<string, byte[]>? byteLoader = null)
    {
        _fontFilePath = fontFilePath;
        _byteLoader = byteLoader ?? (path => File.ReadAllBytes(path));
        _cache = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        // Single underlying face: map every family to it, honoring style via simulation.
        new(FaceName, isBold, isItalic);

    public byte[] GetFont(string faceName)
    {
        if (faceName != FaceName)
            throw new ArgumentException("Unknown font face name.", nameof(faceName));

        lock (_sync)
        {
            if (!_cache.TryGetValue(faceName, out var bytes))
            {
                bytes = _byteLoader(_fontFilePath);
                _cache[faceName] = bytes;
            }

            return bytes;
        }
    }
}
