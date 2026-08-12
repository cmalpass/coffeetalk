namespace CoffeeTalk.Services;

public interface IApplicationDataPathResolver
{
    string RootDirectory { get; }
    string ConfigurationFilePath { get; }
    string ResolveDataPath(string? path, string defaultFileName);
    string ResolveExportPath(string? path, string defaultFileName);
}

public sealed class ApplicationDataPathResolver : IApplicationDataPathResolver
{
    private readonly string _exportDirectory;

    public ApplicationDataPathResolver(string? rootDirectory = null)
    {
        RootDirectory = Path.GetFullPath(rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CoffeeTalk"));
        _exportDirectory = Path.Combine(RootDirectory, "exports");
        ConfigurationFilePath = Path.Combine(RootDirectory, "appsettings.json");
    }

    public string RootDirectory { get; }
    public string ConfigurationFilePath { get; }

    public string ResolveDataPath(string? path, string defaultFileName) =>
        ResolveWithin(RootDirectory, path, defaultFileName);

    public string ResolveExportPath(string? path, string defaultFileName) =>
        ResolveWithin(_exportDirectory, path, defaultFileName);

    private static string ResolveWithin(string root, string? path, string defaultFileName)
    {
        if (string.IsNullOrWhiteSpace(defaultFileName))
            throw new ArgumentException("A default file name is required.", nameof(defaultFileName));

        var relativePath = string.IsNullOrWhiteSpace(path) ? defaultFileName : path;
        if (Path.IsPathRooted(relativePath))
            throw new UnauthorizedAccessException("Rooted paths are not allowed.");

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullPath.Equals(fullRoot, StringComparison.Ordinal) &&
            !fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Path escapes the CoffeeTalk data directory.");
        }

        return fullPath;
    }
}
