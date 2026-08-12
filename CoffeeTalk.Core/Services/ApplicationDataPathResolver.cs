namespace CoffeeTalk.Services;

public interface IApplicationDataPathResolver
{
    string RootDirectory { get; }
    string ConfigurationFilePath { get; }
    string ResolveDataPath(string? path, string defaultFileName);
    string ResolveExportPath(string? path, string defaultFileName);
}

public interface IWorkspacePathResolver : IApplicationDataPathResolver
{
    string BaseRootDirectory { get; }
    string? WorkspaceName { get; }
    void SwitchWorkspace(string? workspaceName);
}

public sealed class ApplicationDataPathResolver : IWorkspacePathResolver
{
    private readonly string _baseRootDirectory;
    private string _exportDirectory = string.Empty;

    public ApplicationDataPathResolver(string? rootDirectory = null, string? workspaceName = null)
    {
        _baseRootDirectory = Path.GetFullPath(rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CoffeeTalk"));
        SwitchWorkspace(workspaceName);
    }

    public string BaseRootDirectory => _baseRootDirectory;
    public string RootDirectory { get; private set; } = string.Empty;
    public string? WorkspaceName { get; private set; }
    public string ConfigurationFilePath { get; private set; } = string.Empty;

    public void SwitchWorkspace(string? workspaceName)
    {
        if (!string.IsNullOrWhiteSpace(workspaceName))
            WorkspaceNameValidator.Validate(workspaceName);

        WorkspaceName = string.IsNullOrWhiteSpace(workspaceName) ? null : workspaceName;
        RootDirectory = WorkspaceName is null
            ? _baseRootDirectory
            : Path.Combine(_baseRootDirectory, "workspaces", WorkspaceName);
        _exportDirectory = Path.Combine(RootDirectory, "exports");
        ConfigurationFilePath = Path.Combine(RootDirectory, "appsettings.json");
    }

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

        var current = fullRoot;
        if (Directory.Exists(current) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            throw new UnauthorizedAccessException("Reparse points are not allowed in the data directory.");
        foreach (var segment in Path.GetRelativePath(fullRoot, fullPath)
                     .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new UnauthorizedAccessException("Reparse points are not allowed in the data directory.");
        }
        return fullPath;
    }
}
