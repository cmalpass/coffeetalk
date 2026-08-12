using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

/// <summary>
/// Convenience facade for command-line and other simple clients. It keeps the
/// storage contract available through <see cref="IMemoryStore"/> while exposing
/// entry-oriented operations.
/// </summary>
public sealed class MemoryService : IDisposable
{
    private readonly LocalMemoryStore _store;

    public MemoryService(IApplicationDataPathResolver paths)
        : this(paths, new ConfigurationService(paths).LoadConfiguration().Memory) { }

    public MemoryService(IApplicationDataPathResolver paths, MemoryConfig config)
        => _store = new LocalMemoryStore(
            paths ?? throw new ArgumentNullException(nameof(paths)),
            config ?? throw new ArgumentNullException(nameof(config)));

    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(
        CancellationToken cancellationToken = default)
        => (await _store.ListAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToEntry)
            .ToList();

    public async Task<IReadOnlyList<MemoryEntry>> SearchAsync(
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var entries = string.IsNullOrWhiteSpace(query)
            ? await _store.ListAsync(cancellationToken).ConfigureAwait(false)
            : await _store.SearchAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false);
        return entries.Select(ToEntry).ToList();
    }

    public async Task<MemoryEntry?> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
        => (await _store.GetAsync(id, cancellationToken).ConfigureAwait(false)) is { } entry
            ? ToEntry(entry)
            : null;

    public Task<MemoryEntry> AddAsync(
        string content,
        string? source = null,
        CancellationToken cancellationToken = default)
        => SaveNewAsync(content, source, cancellationToken);

    public Task<MemoryEntry> AddAsync(
        string content,
        CancellationToken cancellationToken)
        => SaveNewAsync(content, null, cancellationToken);

    public Task<MemoryEntry> AddAsync(
        MemoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return SaveAsync(entry, cancellationToken);
    }

    public async Task<MemoryEntry> EditAsync(
        string id,
        string content,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Memory '{id}' was not found.");
        existing.Content = content;
        if (source is not null)
            existing.Source = source;
        return ToEntry(await _store.SaveAsync(existing, cancellationToken).ConfigureAwait(false));
    }

    public Task<MemoryEntry> EditAsync(
        string id,
        string content,
        CancellationToken cancellationToken)
        => EditAsync(id, content, null, cancellationToken);

    public async Task<MemoryEntry> EditAsync(
        MemoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var existing = await _store.GetAsync(entry.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Memory '{entry.Id}' was not found.");
        existing.Content = entry.Content;
        existing.Source = entry.Source;
        existing.Metadata = new Dictionary<string, string>(entry.Metadata, StringComparer.Ordinal);
        return ToEntry(await _store.SaveAsync(existing, cancellationToken).ConfigureAwait(false));
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(id, cancellationToken);

    public Task<int> PurgeAsync(CancellationToken cancellationToken = default) =>
        PurgeAllAsync(cancellationToken);

    private Task<int> PurgeAllAsync(CancellationToken cancellationToken) =>
        _store.DeleteAllAsync(cancellationToken);

    public void Dispose() => _store.Dispose();

    private async Task<MemoryEntry> SaveNewAsync(
        string content,
        string? source,
        CancellationToken cancellationToken)
    {
        var entry = new MemoryEntry { Content = content, Source = source };
        return await SaveAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MemoryEntry> SaveAsync(
        MemoryEntry entry,
        CancellationToken cancellationToken)
        => ToEntry(await _store.SaveAsync(entry, cancellationToken).ConfigureAwait(false));

    private static MemoryEntry ToEntry(MemoryDto entry) => new()
    {
        SchemaVersion = entry.SchemaVersion,
        Id = entry.Id,
        Content = entry.Content,
        Source = entry.Source,
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt,
        Metadata = new Dictionary<string, string>(
            entry.Metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal)
    };
}
