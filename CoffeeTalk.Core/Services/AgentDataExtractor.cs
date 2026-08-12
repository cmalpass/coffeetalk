using Microsoft.Agents.AI;
using CoffeeTalk.Core.Interfaces;
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
    private readonly IApplicationDataPathResolver _paths;
 private readonly IRetryService _retryService;
 private readonly IOperationalEventSink _eventSink;

 public AgentDataExtractor(
     AIAgent agent,
     StructuredDataConfig config,
     CollaborativeMarkdownDocument doc,
     IRetryService retryService,
     IOperationalEventSink? eventSink = null,
     IApplicationDataPathResolver? paths = null)
 {
     _agent = agent;
     _config = config;
     _doc = doc;
     _paths = paths ?? new ApplicationDataPathResolver();
     _retryService = retryService;
     _eventSink = eventSink ?? NullOperationalEventSink.Instance;
 }

 public AgentDataExtractor(AIAgent agent, StructuredDataConfig config, CollaborativeMarkdownDocument doc, IApplicationDataPathResolver? paths = null)
     : this(agent, config, doc, new RetryService(null), null, paths)
 {
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

    public async Task ExtractAndSaveAsync(List<string> conversationHistory, CancellationToken cancellationToken = default)
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
            var response = await _retryService.ExecuteAsync(
                async cancellationToken => await _agent.RunAsync(prompt, cancellationToken: cancellationToken),
                "Data extraction",
                cancellationToken);

            var json = CleanJson(response.ToString());

            var outputPath = _paths.ResolveDataPath(_config.OutputFile, "data.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            _eventSink.Publish(new OperationalEvent(
                OperationalEventKind.OperationFailure,
                "Data extraction"));
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
