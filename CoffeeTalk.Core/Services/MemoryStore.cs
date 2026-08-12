using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public interface IMemorySearch
{
    Task<IReadOnlyList<MemoryDto>> SearchAsync(
        string query,
        MemorySearchOptions? options = null,
        CancellationToken cancellationToken = default);
}

public interface IMemoryStore : IMemorySearch
{
    Task<MemoryDto> SaveAsync(MemoryDto memory, CancellationToken cancellationToken = default);
    Task<MemoryDto?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryDto>> ListAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);

    Task<MemoryDto> UpsertAsync(MemoryDto memory, CancellationToken cancellationToken = default) =>
        SaveAsync(memory, cancellationToken);

    Task<MemoryDto> AddAsync(MemoryDto memory, CancellationToken cancellationToken = default) =>
        SaveAsync(memory, cancellationToken);
}

public class MemoryStoreCorruptException : Exception
{
    public MemoryStoreCorruptException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class MemoryStoreVersionException : MemoryStoreCorruptException
{
    public MemoryStoreVersionException(int version)
        : base($"Memory schema version {version} is not supported.") { }
}

public sealed class MemoryStoreLimitException : InvalidOperationException
{
    public MemoryStoreLimitException(string message) : base(message) { }
}

public sealed class MemoryDisabledException : InvalidOperationException
{
    public MemoryDisabledException()
        : base("Workspace memory is disabled in application settings.") { }
}

/// <summary>
/// A workspace-local JSON record store with deterministic lexical search.
/// The injected path resolver is deliberately used for every operation so a
/// store follows the currently selected workspace.
/// </summary>
public class LocalMemoryStore : IMemoryStore, IDisposable
{
    private const string DataFile = "memory/memory.json";
    private const int MaxMetadataValueLength = 16 * 1024;
    private readonly IApplicationDataPathResolver _paths;
    private readonly MemoryConfig _config;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public LocalMemoryStore(
        IApplicationDataPathResolver? paths = null,
        MemoryConfig? config = null)
    {
        _paths = paths ?? new ApplicationDataPathResolver();
        _config = config ?? new MemoryConfig();
        ValidateConfig(_config);
    }

    public LocalMemoryStore(MemoryConfig config, IApplicationDataPathResolver? paths = null)
        : this(paths, config) { }

    public async Task<MemoryDto> SaveAsync(
        MemoryDto memory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        EnsureEnabled();
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeForSave(memory);
        var gate = GetGate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var cutoff = GetRetentionCutoff();
            document.Entries.RemoveAll(entry => IsExpired(entry, cutoff));

            var existing = document.Entries.FindIndex(entry =>
                entry.Id.Equals(normalized.Id, StringComparison.Ordinal));
            if (existing >= 0)
            {
                normalized.CreatedAt = document.Entries[existing].CreatedAt;
                document.Entries[existing] = normalized;
            }
            else
            {
                if (document.Entries.Count >= _config.MaxEntries)
                    throw new MemoryStoreLimitException("The workspace memory entry limit has been reached.");
                document.Entries.Add(normalized);
            }

            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return Clone(normalized);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<MemoryDto?> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeId(id);
        var gate = GetGate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var entry = document.Entries.FirstOrDefault(item =>
                item.Id.Equals(normalizedId, StringComparison.Ordinal));
            return entry is null || IsExpired(entry, GetRetentionCutoff()) ? null : Clone(entry);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<MemoryDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cutoff = GetRetentionCutoff();
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return document.Entries
                .Where(entry => !IsExpired(entry, cutoff))
                .OrderByDescending(entry => entry.UpdatedAt)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .Select(Clone)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<MemoryDto>> SearchAsync(
        string query,
        MemorySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Length > _config.MaxQueryLength)
            throw new MemoryStoreLimitException("The memory search query is too long.");

        var tokens = Tokenize(query).Distinct(StringComparer.Ordinal).ToArray();
        if (tokens.Length == 0)
            return Array.Empty<MemoryDto>();

        var limit = options?.Limit ?? _config.MaxResults;
        if (limit <= 0 || limit > _config.MaxResults)
            throw new MemoryStoreLimitException("The memory search result limit is outside the configured limit.");

        var gate = GetGate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var cutoff = GetRetentionCutoff();
            var matches = new List<(MemoryDto Entry, int Score)>();
            foreach (var entry in document.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsExpired(entry, cutoff) ||
                    options?.CreatedAfter is { } after && entry.CreatedAt <= after)
                    continue;

                var counts = Tokenize(SearchableText(entry))
                    .GroupBy(token => token, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
                if (tokens.Any(token => !counts.ContainsKey(token)))
                    continue;

                matches.Add((entry, tokens.Sum(token => counts[token])));
            }

            return matches
                .OrderByDescending(match => match.Score)
                .ThenByDescending(match => match.Entry.UpdatedAt)
                .ThenBy(match => match.Entry.Id, StringComparer.Ordinal)
                .Take(limit)
                .Select(match => Clone(match.Entry))
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeId(id);
        var gate = GetGate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            if (document.Entries.RemoveAll(entry =>
                    entry.Id.Equals(normalizedId, StringComparison.Ordinal)) > 0)
                await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var gate = GetGate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Entries.RemoveAll(entry => IsExpired(entry, GetRetentionCutoff()));
            if (removed > 0)
                await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return removed;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var gate = GetGate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Entries.Count;
            if (removed == 0)
                return 0;
            document.Entries.Clear();
            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return removed;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
    }

    private async Task<MemoryDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath();
        if (!File.Exists(path))
            return new MemoryDocument();
        if (new FileInfo(path).Length > _config.MaxTotalSizeBytes)
            throw new MemoryStoreLimitException("The memory store exceeds the configured size limit.");

        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync<MemoryDocument>(
                stream, _json, cancellationToken).ConfigureAwait(false);
            if (document is null)
                throw new MemoryStoreCorruptException("The memory store is empty.");
            if (document.SchemaVersion != MemorySchema.CurrentVersion)
                throw new MemoryStoreVersionException(document.SchemaVersion);
            if (document.Entries is null)
                throw new MemoryStoreCorruptException("The memory store is missing its entries.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in document.Entries)
            {
                ValidateStoredEntry(entry);
                if (!ids.Add(entry.Id))
                    throw new MemoryStoreCorruptException("The memory store contains duplicate identifiers.");
            }
            if (document.Entries.Count > _config.MaxEntries)
                throw new MemoryStoreLimitException("The memory store exceeds the configured entry limit.");
            return document;
        }
        catch (MemoryStoreCorruptException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new MemoryStoreCorruptException("The memory store JSON is invalid.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new MemoryStoreCorruptException("The memory store JSON is invalid.", ex);
        }
    }

    private async Task WriteDocumentAsync(MemoryDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        document.SchemaVersion = MemorySchema.CurrentVersion;
        var payload = JsonSerializer.SerializeToUtf8Bytes(document, _json);
        if (payload.LongLength > _config.MaxTotalSizeBytes)
            throw new MemoryStoreLimitException("The workspace memory size limit has been reached.");

        var path = ResolvePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private MemoryDto NormalizeForSave(MemoryDto memory)
    {
        if (memory.SchemaVersion != MemorySchema.CurrentVersion)
            throw new MemoryStoreVersionException(memory.SchemaVersion);
        var id = NormalizeId(memory.Id);
        if (string.IsNullOrEmpty(memory.Content))
            throw new ArgumentException("Memory content is required.", nameof(memory));
        if (memory.Content.Length > _config.MaxCharactersPerEntry)
            throw new MemoryStoreLimitException("The memory entry exceeds the configured character limit.");
        if (Encoding.UTF8.GetByteCount(memory.Content) > _config.MaxEntrySizeBytes)
            throw new MemoryStoreLimitException("The memory entry exceeds the configured size limit.");
        if (memory.Metadata is null)
            memory.Metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in memory.Metadata)
        {
            if (pair.Key is null || pair.Value is null || pair.Key.Length > MaxMetadataValueLength ||
                pair.Value.Length > MaxMetadataValueLength)
                throw new MemoryStoreLimitException("Memory metadata is too large.");
        }

        var now = DateTimeOffset.UtcNow;
        return new MemoryDto
        {
            SchemaVersion = MemorySchema.CurrentVersion,
            Id = id,
            Content = memory.Content,
            Source = memory.Source,
            CreatedAt = memory.CreatedAt == default ? now : memory.CreatedAt.ToUniversalTime(),
            UpdatedAt = now,
            Metadata = new Dictionary<string, string>(memory.Metadata, StringComparer.Ordinal)
        };
    }

    private void ValidateStoredEntry(MemoryDto entry)
    {
        if (entry is null)
            throw new MemoryStoreCorruptException("The memory store contains a null entry.");
        if (entry.SchemaVersion != MemorySchema.CurrentVersion)
            throw new MemoryStoreVersionException(entry.SchemaVersion);
        try { NormalizeId(entry.Id); }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            throw new MemoryStoreCorruptException("The memory store contains an unsafe identifier.", ex);
        }
        if (string.IsNullOrEmpty(entry.Content))
            throw new MemoryStoreCorruptException("The memory store contains an empty entry.");
        if (entry.Content.Length > _config.MaxCharactersPerEntry)
            throw new MemoryStoreLimitException("A memory entry exceeds the configured character limit.");
        if (Encoding.UTF8.GetByteCount(entry.Content) > _config.MaxEntrySizeBytes)
            throw new MemoryStoreLimitException("A memory entry exceeds the configured size limit.");
        entry.Metadata ??= new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in entry.Metadata)
        {
            if (pair.Key is null || pair.Value is null || pair.Key.Length > MaxMetadataValueLength ||
                pair.Value.Length > MaxMetadataValueLength)
                throw new MemoryStoreLimitException("Memory metadata is too large.");
        }
    }

    private string ResolvePath() => _paths.ResolveDataPath(DataFile, "memory.json");

    private SemaphoreSlim GetGate() =>
        Gates.GetOrAdd(Path.GetFullPath(ResolvePath()), static _ => new SemaphoreSlim(1, 1));

    private void EnsureEnabled()
    {
        if (!_config.Enabled)
            throw new MemoryDisabledException();
    }

    private DateTimeOffset? GetRetentionCutoff() =>
        _config.RetentionDays > 0
            ? DateTimeOffset.UtcNow.AddDays(-_config.RetentionDays)
            : null;

    private static bool IsExpired(MemoryDto entry, DateTimeOffset? cutoff) =>
        cutoff is { } value && entry.CreatedAt < value;

    private static string SearchableText(MemoryDto entry) =>
        string.Join(' ', new[] { entry.Content, entry.Source }
            .Concat(entry.Metadata?.Values ?? Enumerable.Empty<string>())
            .Where(value => !string.IsNullOrEmpty(value)));

    private static IEnumerable<string> Tokenize(string value)
    {
        var token = new StringBuilder();
        foreach (var character in value.Normalize())
        {
            if (char.IsLetterOrDigit(character))
                token.Append(char.ToLowerInvariant(character));
            else if (token.Length > 0)
            {
                yield return token.ToString();
                token.Clear();
            }
        }
        if (token.Length > 0)
            yield return token.ToString();
    }

    private static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            id.Any(char.IsControl) || id.Length > 256 ||
            id.Contains('/') || id.Contains('\\') || id is "." or "..")
            throw new UnauthorizedAccessException("Memory identifiers must be safe file names.");
        return id;
    }

    private static MemoryDto Clone(MemoryDto memory) => new()
    {
        SchemaVersion = memory.SchemaVersion,
        Id = memory.Id,
        Content = memory.Content,
        Source = memory.Source,
        CreatedAt = memory.CreatedAt,
        UpdatedAt = memory.UpdatedAt,
        Metadata = new Dictionary<string, string>(
            memory.Metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal)
    };

    private static void ValidateConfig(MemoryConfig config)
    {
        if (config.MaxEntries <= 0 || config.MaxCharactersPerEntry <= 0 || config.MaxEntrySizeBytes <= 0 ||
            config.MaxTotalSizeBytes <= 0 || config.MaxQueryLength <= 0 || config.MaxResults <= 0 ||
            config.RetentionDays < 0)
            throw new ArgumentOutOfRangeException(nameof(config), "Memory limits must be positive and retention cannot be negative.");
        if (config.MaxEntrySizeBytes > config.MaxTotalSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(config), "An entry cannot be larger than the total memory limit.");
    }

    private sealed class MemoryDocument
    {
        public int SchemaVersion { get; set; } = MemorySchema.CurrentVersion;
        public List<MemoryDto> Entries { get; set; } = new();
    }
}

public sealed class JsonMemoryStore : LocalMemoryStore
{
    public JsonMemoryStore(IApplicationDataPathResolver? paths = null, MemoryConfig? config = null)
        : base(paths, config) { }

    public JsonMemoryStore(MemoryConfig config, IApplicationDataPathResolver? paths = null)
        : base(paths, config) { }
}
