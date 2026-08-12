namespace CoffeeTalk.Models;

public class PersonaConfig
{
    public string Name { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public List<string>? AllowedTools { get; set; }
}
