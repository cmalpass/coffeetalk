using System.Text.Json;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public interface IWorkspaceService
{
    WorkspaceMetadata Active { get; }
    Task<IReadOnlyList<WorkspaceMetadata>> ListAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceMetadata> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<WorkspaceMetadata> SwitchAsync(string idOrName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string idOrName, CancellationToken cancellationToken = default);
}

public sealed class WorkspaceService : IWorkspaceService
{
    private const string WorkspaceDirectory = "workspaces";
    private const string MetadataFile = "workspace.json";
    private const string ActiveFile = "active-workspace.json";
    private readonly IWorkspacePathResolver _paths;
    private readonly string _workspaceRoot;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public WorkspaceService(IWorkspacePathResolver paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _workspaceRoot = Path.Combine(_paths.BaseRootDirectory, WorkspaceDirectory);
        Active = EnsureActiveWorkspace();
    }

    public WorkspaceMetadata Active { get; private set; }

    public async Task<IReadOnlyList<WorkspaceMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_workspaceRoot);
        var result = new List<WorkspaceMetadata>();
        foreach (var directory in Directory.EnumerateDirectories(_workspaceRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
                continue;
            var metadata = await ReadMetadataAsync(directory, Path.GetFileName(directory), cancellationToken);
            if (metadata is not null)
                result.Add(metadata);
        }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<WorkspaceMetadata> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("A workspace name is required.", nameof(name));
        var id = WorkspaceNameValidator.ToId(normalizedName);
        var path = Path.Combine(_workspaceRoot, id);
        if (Directory.Exists(path))
            throw new InvalidOperationException($"Workspace '{normalizedName}' already exists.");

        Directory.CreateDirectory(path);
        var metadata = new WorkspaceMetadata { Id = id, Name = normalizedName };
        await WriteMetadataAsync(path, metadata, cancellationToken);
        return metadata;
    }

    public async Task<WorkspaceMetadata> SwitchAsync(string idOrName, CancellationToken cancellationToken = default)
    {
        var workspace = await FindAsync(idOrName, cancellationToken)
            ?? throw new KeyNotFoundException($"Workspace '{idOrName}' was not found.");
        Active = workspace;
        _paths.SwitchWorkspace(workspace.Id);
        await File.WriteAllTextAsync(
            Path.Combine(_paths.BaseRootDirectory, ActiveFile),
            JsonSerializer.Serialize(new { id = workspace.Id }, _json),
            cancellationToken);
        return workspace;
    }

    public async Task DeleteAsync(string idOrName, CancellationToken cancellationToken = default)
    {
        var workspace = await FindAsync(idOrName, cancellationToken)
            ?? throw new KeyNotFoundException($"Workspace '{idOrName}' was not found.");
        if (workspace.Id.Equals(Active.Id, StringComparison.Ordinal))
        {
            var replacement = (await ListAsync(cancellationToken))
                .FirstOrDefault(x => !x.Id.Equals(workspace.Id, StringComparison.Ordinal));
            if (replacement is null)
                throw new InvalidOperationException("The only workspace cannot be deleted.");
            await SwitchAsync(replacement.Id, cancellationToken);
        }

        var path = ResolveWorkspacePath(workspace.Id);
        Directory.Delete(path, true);
    }

    private async Task<WorkspaceMetadata?> FindAsync(string idOrName, CancellationToken cancellationToken)
        => (await ListAsync(cancellationToken)).FirstOrDefault(x =>
            x.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase));

    private WorkspaceMetadata EnsureActiveWorkspace()
    {
        Directory.CreateDirectory(_workspaceRoot);
        var activeId = ReadActiveId();
        WorkspaceMetadata? metadata = null;
        if (activeId is not null)
        {
            try
            {
                metadata = ReadMetadataAsync(
                    ResolveWorkspacePath(activeId), activeId, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (UnauthorizedAccessException) { }
        }
        if (metadata is null)
        {
            metadata = ReadMetadataAsync(ResolveWorkspacePath("default"), "default", CancellationToken.None).GetAwaiter().GetResult();
            if (metadata is null)
            {
                var path = ResolveWorkspacePath("default");
                Directory.CreateDirectory(path);
                MigrateLegacyFiles(path);
                metadata = new WorkspaceMetadata { Id = "default", Name = "Default" };
                WriteMetadataAsync(path, metadata, CancellationToken.None).GetAwaiter().GetResult();
            }
            File.WriteAllText(Path.Combine(_paths.BaseRootDirectory, ActiveFile),
                JsonSerializer.Serialize(new { id = metadata.Id }, _json));
        }
        _paths.SwitchWorkspace(metadata.Id);
        return metadata;
    }

    private string? ReadActiveId()
    {
        var path = Path.Combine(_paths.BaseRootDirectory, ActiveFile);
        if (!File.Exists(path))
            return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.GetProperty("id").GetString();
        }
        catch (JsonException) { return null; }
        catch (KeyNotFoundException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private async Task<WorkspaceMetadata?> ReadMetadataAsync(
        string directory, string expectedId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, MetadataFile);
        if (!File.Exists(path))
            return null;
        try
        {
            await using var stream = File.OpenRead(path);
            var metadata = await JsonSerializer.DeserializeAsync<WorkspaceMetadata>(stream, _json, cancellationToken);
            if (metadata is null || string.IsNullOrWhiteSpace(metadata.Id) ||
                !metadata.Id.Equals(expectedId, StringComparison.Ordinal))
                return null;
            WorkspaceNameValidator.Validate(metadata.Id);
            return metadata;
        }
        catch (JsonException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private async Task WriteMetadataAsync(string directory, WorkspaceMetadata metadata, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(directory, MetadataFile),
            JsonSerializer.Serialize(metadata, _json), cancellationToken);
    }

    private string ResolveWorkspacePath(string id)
    {
        WorkspaceNameValidator.Validate(id);
        var path = Path.GetFullPath(Path.Combine(_workspaceRoot, id));
        if (!path.StartsWith(_workspaceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Workspace path escapes the data directory.");
        if (Directory.Exists(path) &&
            File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new UnauthorizedAccessException("Workspace symlinks are not allowed.");
        return path;
    }

    private void MigrateLegacyFiles(string destination)
    {
        foreach (var name in new[] { "appsettings.json", "conversation-history.json" })
        {
            var source = Path.Combine(_paths.BaseRootDirectory, name);
            if (File.Exists(source) &&
                !File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint))
                File.Move(source, Path.Combine(destination, name));
        }
        var exports = Path.Combine(_paths.BaseRootDirectory, "exports");
        if (Directory.Exists(exports) &&
            !File.GetAttributes(exports).HasFlag(FileAttributes.ReparsePoint))
            Directory.Move(exports, Path.Combine(destination, "exports"));
        var conversations = Path.Combine(_paths.BaseRootDirectory, "conversations");
        if (Directory.Exists(conversations) &&
            !File.GetAttributes(conversations).HasFlag(FileAttributes.ReparsePoint))
            Directory.Move(conversations, Path.Combine(destination, "conversations"));
    }
}

public static class WorkspaceNameValidator
{
    public static void Validate(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." ||
            id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            id.Contains('/') || id.Contains('\\'))
            throw new UnauthorizedAccessException("Workspace identifiers must be safe directory names.");
    }

    public static string ToId(string name)
    {
        var id = new string(name.ToLowerInvariant().Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        Validate(id);
        return id;
    }
}
