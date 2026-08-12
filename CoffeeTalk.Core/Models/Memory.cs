namespace CoffeeTalk.Models;

public static class MemorySchema
{
    public const int CurrentVersion = 1;
}

/// <summary>
/// A single piece of workspace-scoped text. Content is untrusted input.
/// </summary>
public class MemoryDto
{
    public int SchemaVersion { get; set; } = MemorySchema.CurrentVersion;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Content { get; set; } = string.Empty;
    public string? Source { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}

// These names make the DTO usable by callers that prefer record/entry terminology
// while retaining one wire representation and one store API.
public class MemoryRecord : MemoryDto { }
public class MemoryEntry : MemoryDto { }
public class Memory : MemoryDto { }

public sealed class MemorySearchOptions
{
    public int? Limit { get; set; }
    public DateTimeOffset? CreatedAfter { get; set; }
}
