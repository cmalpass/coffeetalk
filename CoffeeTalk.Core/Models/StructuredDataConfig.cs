namespace CoffeeTalk.Models;

public class StructuredDataConfig
{
    public bool Enabled { get; set; } = false;
    public string OutputFile { get; set; } = "data.json";
    public string SchemaDescription { get; set; } = "Extract key action items, decisions, and metrics.";
}
