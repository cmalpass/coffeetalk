namespace CoffeeTalk.Models;

/// <summary>Controls the optional, workspace-local textual memory feature.</summary>
public sealed class MemoryConfig
{
    public bool Enabled { get; set; }
    public int MaxEntries { get; set; } = 100;
    public int MaxCharactersPerEntry { get; set; } = 2_000;
    public int MaxEntrySizeBytes { get; set; } = 64 * 1024;
    public int MaxTotalSizeBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxQueryLength { get; set; } = 512;
    public int RecallLimit { get; set; } = 5;
    public int RetentionDays { get; set; } = 30;

    // Compatibility alias for callers that describe recall as a result limit.
    [System.Text.Json.Serialization.JsonIgnore]
    public int MaxResults
    {
        get => RecallLimit;
        set => RecallLimit = value;
    }
}
