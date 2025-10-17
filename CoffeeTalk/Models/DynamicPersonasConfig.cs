namespace CoffeeTalk.Models;

public class DynamicPersonasConfig
{
    // Enable dynamic persona generation
    public bool Enabled { get; set; } = false;

    // Number of personas to generate at runtime (clamped to [2,10])
    public int Count { get; set; } = 3;

    // Mode: "augment" adds to existing personas; "replace" ignores configured personas
    public string Mode { get; set; } = "augment"; // values: augment | replace
}
