namespace CoffeeTalk.Services;

/// <summary>
/// Locates a usable TrueType/OpenType font on the host platform for PDF export.
/// </summary>
internal static class SystemFontDiscovery
{
    /// <summary>
    /// Returns an ordered set of candidate font file paths for the current platform.
    /// </summary>
    public static IEnumerable<string> GetDefaultCandidatePaths()
    {
        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrEmpty(fontsDir))
        {
            foreach (var name in new[] { "arial", "Arial", "LiberationSans-Regular", "DejaVuSans" })
                yield return Path.Combine(fontsDir, name + ".ttf");
        }

        // macOS (Arial ships in the Supplemental folder on modern macOS).
        yield return "/System/Library/Fonts/Supplemental/Arial.ttf";
        yield return "/System/Library/Fonts/Supplemental/Arial Bold.ttf";
        yield return "/System/Library/Fonts/Helvetica.ttc";
        yield return "/System/Library/Fonts/Helvetica.ttf";
        yield return "/Library/Fonts/Arial.ttf";

        // Common Linux font layouts (DejaVu and Liberation are widely installed).
        yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
        yield return "/usr/share/fonts/dejavu/DejaVuSans.ttf";
        yield return "/usr/share/fonts/TTF/DejaVuSans.ttf";
        yield return "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf";
        yield return "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";
        yield return "/usr/share/fonts/liberation2/LiberationSans-Regular.ttf";
        yield return "/usr/share/fonts/liberation/LiberationSans-Regular.ttf";
        yield return "/usr/share/fonts/truetype/msttcorefonts/Arial.ttf";
    }

    /// <summary>
    /// Finds the first existing font file. When <paramref name="candidatePaths"/> is supplied the
    /// search is limited to those paths; otherwise the default platform candidates are tried first,
    /// followed by a bounded scan of standard font directories.
    /// </summary>
    public static string? FindFontFile(IEnumerable<string>? candidatePaths = null)
    {
        if (candidatePaths is not null)
            return candidatePaths.FirstOrDefault(File.Exists);

        var hit = GetDefaultCandidatePaths().FirstOrDefault(File.Exists);
        if (hit is not null)
            return hit;

        return ScanFontDirectories().FirstOrDefault();
    }

    /// <summary>
    /// Like <see cref="FindFontFile(IEnumerable{string})"/> but throws a clear error when no
    /// usable system font can be located.
    /// </summary>
    public static string FindFontFileOrDefault(IEnumerable<string>? candidatePaths = null) =>
        FindFontFile(candidatePaths)
        ?? throw new InvalidOperationException(
            "No usable system font could be located for PDF export. Install a TrueType/OpenType font (such as DejaVu Sans or Liberation Sans) and try again.");

    /// <summary>
    /// Scans standard system font directories for any TrueType/OpenType font file. The result is
    /// lazy so callers only pay for the directories that actually have to be inspected.
    /// </summary>
    public static IEnumerable<string> ScanFontDirectories()
    {
        var roots = new List<string>();
        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrEmpty(fontsDir))
            roots.Add(fontsDir);
        roots.Add("/System/Library/Fonts/Supplemental");
        roots.Add("/System/Library/Fonts");
        roots.Add("/Library/Fonts");
        roots.Add("/usr/local/share/fonts");
        roots.Add("/usr/share/fonts");

        foreach (var root in roots.Distinct())
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }
}
