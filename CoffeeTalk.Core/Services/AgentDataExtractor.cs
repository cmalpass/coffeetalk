using Microsoft.Agents.AI;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

/// <summary>
/// Agent responsible for extracting structured data from the conversation.
/// </summary>
public class AgentDataExtractor
{
    private readonly AIAgent _agent;
    private readonly StructuredDataConfig _config;
    private readonly CollaborativeMarkdownDocument _doc;

    public AgentDataExtractor(AIAgent agent, StructuredDataConfig config, CollaborativeMarkdownDocument doc)
    {
        _agent = agent;
        _config = config;
        _doc = doc;
    }

    public static string BuildSystemPrompt(StructuredDataConfig config)
    {
        return $@"You are a data extraction specialist.
Your goal is to extract structured data from the conversation and document state based on the following schema description:
'{config.SchemaDescription}'

Output Requirement:
- Return ONLY valid JSON.
- Do not add markdown formatting (like ```json).
- Do not add conversational text.
- If data is missing, use null or empty strings.";
    }

    public async Task ExtractAndSaveAsync(List<string> conversationHistory)
    {
        // UI notification should be handled by the caller or injected UI, but for now we just process.
        // Since we are moving this to Core, we remove AnsiConsole calls.
        // In a real refactor, we would inject IUserInterface here as well, or return the result.
        // For simplicity, we will just do the work and console output will be lost unless we inject UI.

        // TODO: Inject IUserInterface if feedback is needed.
        // For now, we assume this is a background task or the caller handles notifications.

        var historyText = string.Join("\n", conversationHistory.TakeLast(20)); // Last 20 messages
        var docContent = _doc.GetContent();

        var prompt = $@"
Document Content:
{docContent}

Recent Conversation:
{historyText}

Based on the schema description '{_config.SchemaDescription}', extract the data into a JSON object.";

        try
        {
            var response = await RetryHandler.ExecuteWithRetryAsync(
                async () => await _agent.RunAsync(prompt),
                "Data extraction");

            var json = CleanJson(response.ToString());

            await File.WriteAllTextAsync(_config.OutputFile, json);
        }
        catch (Exception)
        {
            // Log error if logger available
        }
    }

    private string CleanJson(string output)
    {
        output = output.Trim();
        if (output.StartsWith("```json")) output = output.Substring(7);
        if (output.StartsWith("```")) output = output.Substring(3);
        if (output.EndsWith("```")) output = output.Substring(0, output.Length - 3);
        return output.Trim();
    }
}
