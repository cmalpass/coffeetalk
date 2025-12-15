using Microsoft.Agents.AI;
using CoffeeTalk.Models;
using Spectre.Console;
using System.Text.Json;

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
        AnsiConsole.MarkupLine("\n[cyan]📊 Extracting structured data...[/]");

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
            AnsiConsole.MarkupLine($"[green]✓ Structured data saved to {_config.OutputFile}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to extract structured data: {ex.Message}[/]");
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
