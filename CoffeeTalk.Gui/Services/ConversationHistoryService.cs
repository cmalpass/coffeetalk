using System.Text.Json;
using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Gui.Services;

public sealed class ConversationHistoryService
{
    private readonly ConversationPersistenceService _persistence;
    private readonly IApplicationDataPathResolver _paths;

    public ConversationHistoryService(IApplicationDataPathResolver? paths = null)
    {
        _paths = paths ?? new ApplicationDataPathResolver();
        _persistence = new ConversationPersistenceService(_paths);
    }

    public IReadOnlyList<ConversationRecord> Recent(int count = 10)
        => RecentAsync(count).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<ConversationRecord>> RecentAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await MigrateLegacyAsync(cancellationToken);
        var states = await _persistence.ListAsync(cancellationToken);
        return states.Take(count).Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<ConversationRecord>> AllAsync(CancellationToken cancellationToken = default)
        => (await GetStatesAsync(cancellationToken)).Select(ToRecord).ToList();

    public async Task<ConversationAnalyticsSummary> SummaryAsync(CancellationToken cancellationToken = default)
        => ConversationMetricsAggregator.Summarize(await GetStatesAsync(cancellationToken));

    public Task<string> SaveAsync(ConversationRecord record, CancellationToken cancellationToken = default)
        => _persistence.SaveAsync(ToState(record), cancellationToken);

    public Task<ConversationRecord> ResumeAsync(string id, CancellationToken cancellationToken = default)
        => ResumeCoreAsync(id, cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => _persistence.DeleteAsync(id, cancellationToken);

    public string Add(ConversationRecord record)
        => SaveAsync(record).GetAwaiter().GetResult();

    private async Task<ConversationRecord> ResumeCoreAsync(string id, CancellationToken cancellationToken)
        => ToRecord(await _persistence.ResumeAsync(id, cancellationToken));

    private async Task<IReadOnlyList<ConversationState>> MigrateLegacyAsync(CancellationToken cancellationToken)
    {
        var path = _paths.ResolveDataPath("conversation-history.json", "conversation-history.json");
        if (!File.Exists(path))
            return Array.Empty<ConversationState>();

        try
        {
            var records = JsonSerializer.Deserialize<List<ConversationRecord>>(
                await File.ReadAllTextAsync(path, cancellationToken),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
            var migrated = new List<ConversationState>();
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = ToState(record);
                await _persistence.SaveAsync(state, cancellationToken);
                migrated.Add(state);
            }

            return migrated;
        }
        catch (JsonException ex)
        {
            throw new ConversationStateCorruptException("Legacy conversation history JSON is invalid.", ex);
        }
    }

    private async Task<IReadOnlyList<ConversationState>> GetStatesAsync(CancellationToken cancellationToken)
    {
        await MigrateLegacyAsync(cancellationToken);
        return await _persistence.ListAsync(cancellationToken);
    }

    private static ConversationState ToState(ConversationRecord record) => new()
    {
        Id = record.Id,
        Topic = record.Topic,
        StartedAt = new DateTimeOffset(record.StartedAt),
        CompletedAt = record.CompletedAt.HasValue ? new DateTimeOffset(record.CompletedAt.Value) : null,
        Status = record.Status,
        DocumentContent = record.DocumentContent,
        Participants = record.Personas.Select(name => new ConversationParticipant { Name = name }).ToList(),
        Messages = record.Messages.Select(message => new ConversationMessage
        {
            Sender = message.Sender, Content = message.Content, IsSystem = message.IsSystem,
            IsError = message.IsError, IsDivider = message.IsDivider, Timestamp = new DateTimeOffset(message.Timestamp)
        }).ToList(),
        Metadata = new Dictionary<string, string>(record.Metadata, StringComparer.Ordinal)
    };

    private static ConversationRecord ToRecord(ConversationState state) => new()
    {
        Id = state.Id,
        Topic = state.Topic,
        StartedAt = state.StartedAt.LocalDateTime,
        CompletedAt = state.CompletedAt?.LocalDateTime,
        Status = state.Status,
        MessageCount = state.Messages.Count(message => !message.IsSystem && !message.IsDivider),
        Personas = state.Participants.Select(participant => participant.Name).ToList(),
        DocumentContent = state.DocumentContent,
        Metadata = new Dictionary<string, string>(state.Metadata, StringComparer.Ordinal),
        Metrics = state.Metrics,
        Messages = state.Messages.Select(message => new ChatMessage
        {
            Sender = message.Sender, Content = message.Content, IsSystem = message.IsSystem,
            IsError = message.IsError, IsDivider = message.IsDivider, Timestamp = message.Timestamp.LocalDateTime
        }).ToList()
    };
}
