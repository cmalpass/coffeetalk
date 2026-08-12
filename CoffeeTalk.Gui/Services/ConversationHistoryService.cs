using System.Text.Json;
using CoffeeTalk.Services;

namespace CoffeeTalk.Gui.Services;

public sealed class ConversationHistoryService
{
    private readonly string _historyPath;
    private readonly object _sync = new();
    private readonly List<ConversationRecord> _records;

    public ConversationHistoryService(IApplicationDataPathResolver? paths = null)
    {
        var resolver = paths ?? new ApplicationDataPathResolver();
        _historyPath = resolver.ResolveDataPath("conversation-history.json", "conversation-history.json");
        _records = Load();
    }

    public IReadOnlyList<ConversationRecord> Recent(int count = 10)
    {
        lock (_sync)
        {
            return _records
                .OrderByDescending(record => record.StartedAt)
                .Take(count)
                .Select(Clone)
                .ToList();
        }
    }

    public void Add(ConversationRecord record)
    {
        lock (_sync)
        {
            _records.Add(Clone(record));
            Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
            File.WriteAllText(_historyPath, JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private List<ConversationRecord> Load()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                return new();
            }

            return JsonSerializer.Deserialize<List<ConversationRecord>>(File.ReadAllText(_historyPath)) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
        catch (IOException)
        {
            return new();
        }
    }

    private static ConversationRecord Clone(ConversationRecord record) => new()
    {
        Topic = record.Topic,
        StartedAt = record.StartedAt,
        CompletedAt = record.CompletedAt,
        Status = record.Status,
        MessageCount = record.MessageCount,
        Personas = record.Personas.ToList(),
        Messages = record.Messages.Select(message => new ChatMessage
        {
            Sender = message.Sender,
            Content = message.Content,
            IsSystem = message.IsSystem,
            IsError = message.IsError,
            IsDivider = message.IsDivider,
            Timestamp = message.Timestamp
        }).ToList()
    };
}
