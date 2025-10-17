namespace CoffeeTalk.Models;

public class ToolsConfig
{
    // If true, allow persona fallback tool JSON execution when native tool calls are unavailable
    public bool EnableFallbackJsonTools { get; set; } = true;

    // If false, allow conversation to proceed even if tools verification fails
    public bool RequireToolsVerification { get; set; } = true;
}
