using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Gui.Services;

public sealed class MemoryRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Preview => Content.ReplaceLineEndings(" ").Trim();
}

public sealed class MemorySettings
{
    public bool Enabled { get; set; }
}

public sealed record MemoryStoreStatus(
    string WorkspaceName,
    int MemoryCount,
    bool Enabled,
    bool HasData);

public interface IMemoryStoreService
{
    Task<IReadOnlyList<MemoryRecord>> SearchAsync(
        string? query = null,
        CancellationToken cancellationToken = default);

    Task<MemoryRecord> AddAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        MemoryRecord memory,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task PurgeAsync(CancellationToken cancellationToken = default);
    Task<MemorySettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(MemorySettings settings, CancellationToken cancellationToken = default);
    Task<MemoryStoreStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// GUI adapter over the core workspace-local memory store.
/// </summary>
public sealed class MemoryStoreService : IMemoryStoreService
{
    private readonly IWorkspacePathResolver _paths;
    private readonly AppState _appState;

    public MemoryStoreService(IWorkspacePathResolver paths, AppState appState)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _appState = appState ?? throw new ArgumentNullException(nameof(appState));
    }

    public async Task<IReadOnlyList<MemoryRecord>> SearchAsync(
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        using var store = CreateStore();
        IReadOnlyList<MemoryEntry> entries;
        if (string.IsNullOrWhiteSpace(query))
        {
            entries = await store.ListAsync(cancellationToken);
        }
        else if (_appState.Settings.Memory.Enabled)
        {
            entries = await store.SearchAsync(query.Trim(), cancellationToken: cancellationToken);
        }
        else
        {
            // Browsing and local filtering remain available while recall is opted out.
            entries = (await store.ListAsync(cancellationToken))
                .Where(entry =>
                    entry.Content.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    (entry.Source?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ?? false) ||
                    entry.Metadata.Values.Any(value => value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return entries.Select(ToRecord).ToList();
    }

    public async Task<MemoryRecord> AddAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        using var store = CreateStore();
        var entry = await store.AddAsync(new MemoryEntry
        {
            Content = content.Trim(),
            Source = "gui",
            Metadata = CreateMetadata(title)
        }, cancellationToken);
        return ToRecord(entry);
    }

    public async Task UpdateAsync(
        MemoryRecord memory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        using var store = CreateStore();
        var entry = await store.GetAsync(memory.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Memory '{memory.Id}' was not found.");
        entry.Content = memory.Content.Trim();
        entry.Source = "gui";
        entry.Metadata = CreateMetadata(memory.Title);
        await store.EditAsync(entry, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using var store = CreateStore();
        await store.DeleteAsync(id, cancellationToken);
    }

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        using var store = CreateStore();
        await store.PurgeAsync(cancellationToken);
    }

    public Task<MemorySettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new MemorySettings { Enabled = _appState.Settings.Memory.Enabled });

    public async Task SaveSettingsAsync(
        MemorySettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        _appState.Settings.Memory.Enabled = settings.Enabled;
        await _appState.SaveSettingsAsync();
    }

    public async Task<MemoryStoreStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var entries = await SearchAsync(cancellationToken: cancellationToken);
        var workspace = string.IsNullOrWhiteSpace(_paths.WorkspaceName)
            ? "Default workspace"
            : _paths.WorkspaceName!;
        return new MemoryStoreStatus(
            workspace,
            entries.Count,
            _appState.Settings.Memory.Enabled,
            entries.Count > 0);
    }

    private MemoryService CreateStore()
        => new(_paths, _appState.Settings.Memory);

    private static MemoryRecord ToRecord(MemoryEntry entry) => new()
    {
        Id = entry.Id,
        Title = entry.Metadata.TryGetValue("title", out var title) ? title : entry.Source ?? "Untitled memory",
        Content = entry.Content,
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt
    };

    private static Dictionary<string, string> CreateMetadata(string title)
        => string.IsNullOrWhiteSpace(title)
            ? new(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = title.Trim() };
}
