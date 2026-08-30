using System.Text.Json;
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
 private readonly RateLimiter? _rateLimiter;

 public AgentDataExtractor(
     AIAgent agent,
     StructuredDataConfig config,
     CollaborativeMarkdownDocument doc,
     IRetryService retryService,
     IOperationalEventSink? eventSink = null,
     IApplicationDataPathResolver? paths = null,
     RateLimiter? rateLimiter = null)
 {
     _agent = agent;
     _config = config;
     _doc = doc;
     _paths = paths ?? new ApplicationDataPathResolver();
     _retryService = retryService;
     _eventSink = eventSink ?? NullOperationalEventSink.Instance;
     _rateLimiter = rateLimiter;
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
        var historyText = string.Join("\n", conversationHistory.TakeLast(20)); // Last 20 messages

        try
        {
            var json = await ExtractValidatedJsonAsync(historyText, cancellationToken);
            if (json is null)
            {
                // Malformed output exhausted the bounded retry budget; failure was surfaced via
                // DataExtractionFailed and no corrupt data file was written.
                return;
            }

            _rateLimiter?.AccountAdditionalTokens(_rateLimiter.EstimateTokens(json));

            var outputPath = _paths.ResolveDataPath(_config.OutputFile, "data.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _eventSink.Publish(new OperationalEvent(
                OperationalEventKind.OperationFailure,
                "Data extraction")
            {
                Exception = ex
            });
        }
    }

    private async Task<string?> ExtractValidatedJsonAsync(string historyText, CancellationToken cancellationToken)
    {
        var docContent = _doc.GetContent();

        var prompt = $@"
Document Content:
{docContent}

Recent Conversation:
{historyText}

Based on the schema description '{_config.SchemaDescription}', extract the data into a JSON object.";

        var maxAttempts = 2; // Initial + one bounded re-prompt on malformed output.
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_rateLimiter != null)
                await _rateLimiter.ThrottleAsync(_rateLimiter.EstimateTokens(prompt), cancellationToken);

            var response = await _retryService.ExecuteAsync(
                async cancellationToken => await _agent.RunAsync(prompt, cancellationToken: cancellationToken),
                "Data extraction",
                cancellationToken: cancellationToken);

            var json = CleanJson(response.ToString());

            if (IsValidJson(json))
                return json;

            if (attempt < maxAttempts - 1)
            {
                _eventSink.Publish(new OperationalEvent(
                    OperationalEventKind.DataExtractionRetry,
                    "Data extraction",
                    attempt + 1,
                    maxAttempts)
                {
                    Reason = "Malformed output is not valid JSON; re-prompting the model."
                });

                prompt = $@"
Document Content:
{docContent}

Recent Conversation:
{historyText}

Based on the schema description '{_config.SchemaDescription}', extract the data into a JSON object.

Your previous response was NOT valid JSON and was rejected. Return ONLY a single valid JSON object, with no markdown code fences and no additional text:";
            }
        }

        // All bounded attempts produced malformed JSON. Do NOT write a corrupt file.
        _eventSink.Publish(new OperationalEvent(
            OperationalEventKind.DataExtractionFailed,
            "Data extraction",
            maxAttempts,
            maxAttempts)
        {
            Reason = "Model repeatedly returned output that is not valid JSON; no data file was written."
        });

        return null;
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object
                || document.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string CleanJson(string output)
    {
        output = output.Trim();
        if (output.StartsWith("```json", StringComparison.Ordinal)) output = output[7..];
        if (output.StartsWith("```", StringComparison.Ordinal)) output = output[3..];
        if (output.EndsWith("```", StringComparison.Ordinal)) output = output[..^3];
        return output.Trim();
    }
}
