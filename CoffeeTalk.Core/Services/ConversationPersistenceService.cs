using System.Text.Json;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public interface IConversationPersistenceService
{
    Task<string> SaveAsync(ConversationState state, CancellationToken cancellationToken = default);
    Task<ConversationState> ResumeAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationState>> ListAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public class ConversationStateCorruptException : Exception
{
    public ConversationStateCorruptException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class ConversationStateVersionException : ConversationStateCorruptException
{
    public ConversationStateVersionException(int version)
        : base($"Conversation state schema version {version} is not supported.") { }
}

public sealed class ConversationPersistenceService : IConversationPersistenceService
{
    private const string DirectoryName = "conversations";
    private readonly IApplicationDataPathResolver _paths;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ConversationPersistenceService(IApplicationDataPathResolver? paths = null)
        => _paths = paths ?? new ApplicationDataPathResolver();

    public async Task<string> SaveAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var id = NormalizeId(state.Id);
        state.Id = id;
        state.SchemaVersion = ConversationStateSchema.CurrentVersion;
        state.Metrics = ConversationMetricsCalculator.Calculate(state);
        var path = PathFor(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, state, _json, cancellationToken);
            File.Move(temporary, path, true);
            return id;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public async Task<ConversationState> ResumeAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(NormalizeId(id));
        if (!File.Exists(path))
            throw new FileNotFoundException("Conversation state was not found.", path);
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var state = await JsonSerializer.DeserializeAsync<ConversationState>(stream, _json, cancellationToken)
                ?? throw new ConversationStateCorruptException("Conversation state is empty.");
            if (state.SchemaVersion != ConversationStateSchema.CurrentVersion)
                throw new ConversationStateVersionException(state.SchemaVersion);
            if (state.Messages is null || state.Participants is null || state.Metadata is null ||
                string.IsNullOrWhiteSpace(state.Topic))
                throw new ConversationStateCorruptException("Conversation state is missing required fields.");
            state.Id = NormalizeId(state.Id);
            if (!state.Id.Equals(id, StringComparison.Ordinal))
                throw new ConversationStateCorruptException("Conversation state identifier does not match its file.");
            state.Metrics = ConversationMetricsCalculator.Calculate(state);
            return state;
        }
        catch (ConversationStateCorruptException) { throw; }
        catch (JsonException ex) { throw new ConversationStateCorruptException("Conversation state JSON is invalid.", ex); }
        catch (NotSupportedException ex) { throw new ConversationStateCorruptException("Conversation state JSON is invalid.", ex); }
    }

    public async Task<IReadOnlyList<ConversationState>> ListAsync(CancellationToken cancellationToken = default)
    {
        var directory = _paths.ResolveDataPath(DirectoryName, "conversation.json");
        if (!Directory.Exists(directory))
            return Array.Empty<ConversationState>();
        var results = new List<ConversationState>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                results.Add(await ResumeAsync(Path.GetFileNameWithoutExtension(path), cancellationToken));
            }
            catch (ConversationStateCorruptException)
            {
                // A damaged entry must not hide healthy conversations from list consumers.
            }
        }
        return results.OrderByDescending(x => x.StartedAt).ToList();
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = PathFor(NormalizeId(id));
        if (!File.Exists(path))
            throw new FileNotFoundException("Conversation state was not found.", path);
        File.Delete(path);
        return Task.CompletedTask;
    }

    private string PathFor(string id) =>
        _paths.ResolveDataPath($"{DirectoryName}/{id}.json", "conversation.json");

    private static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            id.Contains('/') || id.Contains('\\') || id is "." or "..")
            throw new UnauthorizedAccessException("Conversation identifiers must be safe file names.");
        return id;
    }
}
