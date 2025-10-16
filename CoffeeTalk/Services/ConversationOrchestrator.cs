using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public class ConversationOrchestrator
{
    private readonly List<PersonaAgent> _agents = new();
    private readonly int _maxTurns;
    private readonly bool _showThinking;

    public ConversationOrchestrator(Kernel kernel, AppSettings settings)
    {
        _maxTurns = settings.MaxConversationTurns;
        _showThinking = settings.ShowThinking;

        // Create an agent for each persona
        foreach (var persona in settings.Personas)
        {
            _agents.Add(new PersonaAgent(kernel, persona));
        }
    }

    public async Task StartConversationAsync(string topic)
    {
        if (_agents.Count == 0)
        {
            Console.WriteLine("No personas configured. Please add personas to appsettings.json");
            return;
        }

        Console.WriteLine($"\n🎯 Topic: {topic}\n");
        Console.WriteLine($"Participants: {string.Join(", ", _agents.Select(a => a.Name))}\n");
        Console.WriteLine("Starting conversation...\n");
        Console.WriteLine(new string('=', 80));

        var conversationHistory = new List<string>();
        var currentMessage = $"Let's discuss: {topic}";
        
        for (int turn = 0; turn < _maxTurns; turn++)
        {
            foreach (var agent in _agents)
            {
                try
                {
                    Console.WriteLine($"\n💬 {agent.Name}:");
                    
                    if (_showThinking)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("  [Thinking...]");
                        Console.ResetColor();
                    }

                    var response = await agent.RespondAsync(currentMessage, conversationHistory);
                    Console.WriteLine($"  {response}");
                    
                    conversationHistory.Add($"{agent.Name}: {response}");
                    currentMessage = response;

                    // Check if the conversation goal seems to be reached
                    if (IsConversationComplete(response, turn))
                    {
                        Console.WriteLine($"\n{new string('=', 80)}");
                        Console.WriteLine("\n✅ Conversation goal appears to be reached!");
                        Console.WriteLine($"Total turns: {turn + 1} (across {_agents.Count} personas)");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ❌ Error: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine($"\n{new string('-', 80)}");
        }

        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine($"\n⏱️  Maximum turns ({_maxTurns}) reached. Conversation ended.");
    }

    private bool IsConversationComplete(string response, int turn)
    {
        // Simple heuristic: check if response contains conclusion indicators
        // and we're at least a few turns in
        if (turn < 2) return false;

        var completionIndicators = new[]
        {
            "in conclusion",
            "to summarize",
            "final thought",
            "we've reached",
            "this concludes",
            "agreed upon",
            "consensus",
            "let's wrap",
            "final decision"
        };

        var lowerResponse = response.ToLower();
        return completionIndicators.Any(indicator => lowerResponse.Contains(indicator));
    }
}

public class PersonaAgent
{
    private readonly Kernel _kernel;
    private readonly PersonaConfig _persona;
    private readonly ChatHistory _chatHistory;

    public string Name => _persona.Name;

    public PersonaAgent(Kernel kernel, PersonaConfig persona)
    {
        _kernel = kernel;
        _persona = persona;
        _chatHistory = new ChatHistory();
        _chatHistory.AddSystemMessage(persona.SystemPrompt);
    }

    public async Task<string> RespondAsync(string currentMessage, List<string> conversationHistory)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        // Build context from recent conversation history (last 5 messages)
        var recentHistory = conversationHistory.TakeLast(5).ToList();
        var contextMessage = recentHistory.Count > 0
            ? $"Recent conversation:\n{string.Join("\n", recentHistory)}\n\nCurrent message: {currentMessage}"
            : currentMessage;

        _chatHistory.AddUserMessage(contextMessage);

        var response = await chatService.GetChatMessageContentAsync(_chatHistory);
        var responseText = response.Content ?? "I have no response.";

        _chatHistory.AddAssistantMessage(responseText);

        return responseText;
    }
}
