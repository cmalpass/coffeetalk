using Microsoft.Agents.AI;
using CoffeeTalk.Models;
using System.Text.RegularExpressions;

namespace CoffeeTalk.Services;

/// <summary>
/// Orchestrator agent that decides which persona should speak next using Microsoft Agent Framework
/// </summary>
public class AgentOrchestrator
{
    private readonly AIAgent _agent;
    private readonly OrchestratorConfig _config;
    private readonly CollaborativeMarkdownDocument _doc;
    private readonly Dictionary<string, int> _speakerCount = new();
    private readonly List<AgentPersona> _availablePersonas;

    public AgentOrchestrator(AIAgent agent, OrchestratorConfig config, CollaborativeMarkdownDocument doc, List<AgentPersona> personas)
    {
        _agent = agent;
        _config = config;
        _doc = doc;
        _availablePersonas = personas;

        // Initialize speaker count
        foreach (var persona in personas)
        {
            _speakerCount[persona.Name] = 0;
        }
    }

    public static string BuildSystemPrompt(OrchestratorConfig config, List<AgentPersona> personas)
    {
        var basePrompt = config.BaseSystemPrompt ?? OrchestratorConfig.DefaultBaseSystemPrompt;
        var prompt = basePrompt + "\n\n";

        // Add dynamically generated persona descriptions
        prompt += "Available personas:\n";
        foreach (var persona in personas)
        {
            prompt += $"- {persona.Name}: ";
            
            // Extract key characteristics from system prompt
            var description = ExtractPersonaDescription(persona.SystemPrompt);
            prompt += description + "\n";
        }
        prompt += "\n";

        // Add response format instructions
        prompt += @"You must decide:
1. Whether the conversation should continue or conclude
2. If continuing, which persona should speak next

Response format:
Line 1: Either a persona name OR 'CONCLUDE'
Line 2 (optional): Brief reason for your decision

Decision criteria for CONCLUDE:
- Document has substantive content aligned with the consensus template
- Key viewpoints have been heard (check participation balance)
- A clear position/recommendation has emerged
- Remaining turns are insufficient for meaningful additions
- DO NOT conclude prematurely just because personas agree on one point

Example (continue):
";
        if (personas.Count > 0)
        {
            prompt += personas[0].Name + "\n";
            prompt += "Reason: Need to address trade-offs section\n\n";
        }
        prompt += @"Example (conclude):
CONCLUDE
Reason: Document complete, all personas contributed, clear consensus reached";

        return prompt;
    }

    private static string ExtractPersonaDescription(string systemPrompt)
    {
        // Try to extract the key description from "You are [Name], [description]"
        var match = Regex.Match(systemPrompt, @"You are [^,]+,\s*(.+?)(?:\.|You|Your|\n)", RegexOptions.Singleline);
        if (match.Success)
        {
            var desc = match.Groups[1].Value.Trim();
            // Limit to first sentence or ~100 chars for conciseness
            var firstSentence = desc.Split('.')[0];
            if (firstSentence.Length > 150)
            {
                firstSentence = firstSentence.Substring(0, 147) + "...";
            }
            return firstSentence;
        }

        // Fallback: just take first ~100 chars of system prompt
        var fallback = systemPrompt.Length > 100 ? systemPrompt.Substring(0, 97) + "..." : systemPrompt;
        return fallback;
    }

    public async Task<string> SummarizeAsync(string historyText)
    {
        var prompt = $"Summarize the following conversation history into a single concise paragraph. Capture key points, decisions, and arguments. Do not lose critical context.\n\nHistory:\n{historyText}";
        var response = await RetryHandler.ExecuteWithRetryAsync(
            async () => await _agent.RunAsync(prompt),
            "Orchestrator summarization");
        return response.ToString();
    }

    public async Task<AgentPersona?> SelectNextSpeakerAsync(
        string currentMessage,
        List<string> conversationHistory,
        int turnsRemaining)
    {
        // Build context for orchestrator
        var context = BuildOrchestratorContext(currentMessage, conversationHistory, turnsRemaining);

        // Execute with retry logic for rate limiting (HTTP 429)
        var response = await RetryHandler.ExecuteWithRetryAsync(
            async () => await _agent.RunAsync(context),
            "Orchestrator selection");
        var responseText = response.ToString();

        // Check if orchestrator signals conclusion
        if (ShouldConclude(responseText))
        {
            // Extract reason if present
            var reasonMatch = Regex.Match(responseText, @"Reason:\s*(.+)", RegexOptions.IgnoreCase);
            Console.ForegroundColor = ConsoleColor.Yellow;
            if (reasonMatch.Success)
            {
                Console.WriteLine($"  [Orchestrator: {reasonMatch.Groups[1].Value.Trim()}]");
            }
            else
            {
                Console.WriteLine("  [Orchestrator: Conversation complete]");
            }
            Console.ResetColor();
            return null; // Signal conversation end
        }

        // Parse the response to extract persona name
        var selectedPersona = ParsePersonaSelection(responseText);
        
        if (selectedPersona != null)
        {
            _speakerCount[selectedPersona.Name]++;
            
            // Extract reason if present
            var reasonMatch = Regex.Match(responseText, @"Reason:\s*(.+)", RegexOptions.IgnoreCase);
            if (reasonMatch.Success)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [Orchestrator: {reasonMatch.Groups[1].Value.Trim()}]");
                Console.ResetColor();
            }
        }

        return selectedPersona;
    }

    private string BuildOrchestratorContext(string currentMessage, List<string> history, int turnsRemaining)
    {
        var context = $"Current topic/message: {currentMessage}\n\n";

        // Add recent history
        if (history.Count > 0)
        {
            var recentHistory = history.TakeLast(5);
            context += "Recent conversation:\n" + string.Join("\n", recentHistory) + "\n\n";
        }

        // Add document state
        var headings = _doc.ListHeadings();
        context += "Current document state:\n";
        context += string.IsNullOrWhiteSpace(headings) ? "[Document is empty]\n" : headings + "\n";
        context += "\n";

        // Add participation stats
        context += "Speaker participation count:\n";
        foreach (var kvp in _speakerCount.OrderBy(x => x.Value))
        {
            context += $"- {kvp.Key}: {kvp.Value} time(s)\n";
        }
        context += "\n";

        // Add available personas with their expertise
        context += "Available personas:\n";
        foreach (var persona in _availablePersonas)
        {
            context += $"- {persona.Name}\n";
        }
        context += "\n";

        // Add urgency if needed
        if (turnsRemaining <= 3)
        {
            context += $"⚠️ URGENT: Only {turnsRemaining} turn(s) remaining. Select someone who can help wrap up and conclude.\n\n";
        }

        context += "Who should speak next?";
        
        return context;
    }

    private AgentPersona? ParsePersonaSelection(string response)
    {
        // Try to find persona name in the first line
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;

        var firstLine = lines[0].Trim();
        
        // Try exact match first
        var match = _availablePersonas.FirstOrDefault(p => 
            p.Name.Equals(firstLine, StringComparison.OrdinalIgnoreCase));
        
        if (match != null) return match;

        // Try partial match
        foreach (var persona in _availablePersonas)
        {
            if (firstLine.Contains(persona.Name, StringComparison.OrdinalIgnoreCase))
            {
                return persona;
            }
        }

        // Fallback: find any persona name mentioned in response
        foreach (var persona in _availablePersonas)
        {
            if (response.Contains(persona.Name, StringComparison.OrdinalIgnoreCase))
            {
                return persona;
            }
        }

        return null;
    }

    private bool ShouldConclude(string orchestratorResponse)
    {
        // Orchestrator explicitly signals conclusion with 'CONCLUDE'
        var lines = orchestratorResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return false;
        
        var firstLine = lines[0].Trim();
        return firstLine.Equals("CONCLUDE", StringComparison.OrdinalIgnoreCase);
    }
}
