using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;
using System.Runtime.CompilerServices;

namespace CoffeeTalk.Services;

/// <summary>
/// Represents a persona implemented using Microsoft Agent Framework
/// </summary>
public class AgentPersona
{
    private readonly AIAgent _agent;
    private readonly PersonaConfig _config;
    private readonly CollaborativeMarkdownDocument _doc;
    private readonly RateLimiter? _rateLimiter;
    private readonly int _maxTurns;
    private readonly int _agentCount;
    private readonly IRetryService _retryService;
    private readonly LlmProviderConfig _providerConfig;
    private readonly IOperationalEventSink _eventSink;
    private AgentThread? _thread;
    private bool _threadInitialized;

    public string Name => _config.Name;
    public string SystemPrompt => _config.SystemPrompt;
    public IReadOnlyList<string> EffectiveToolNames { get; }

    public AgentPersona(
        AIAgent agent,
        PersonaConfig config,
        CollaborativeMarkdownDocument doc,
        RateLimiter? rateLimiter,
        int maxTurns,
        int agentCount,
        IRetryService retryService,
        IReadOnlyCollection<string>? effectiveToolNames = null,
        LlmProviderConfig? providerConfig = null,
        IOperationalEventSink? eventSink = null)
    {
        _agent = agent;
        _config = config;
        _doc = doc;
        _rateLimiter = rateLimiter;
        _maxTurns = maxTurns;
        _agentCount = agentCount;
        _retryService = retryService;
        _providerConfig = providerConfig ?? new LlmProviderConfig();
        _eventSink = eventSink ?? NullOperationalEventSink.Instance;
        EffectiveToolNames = effectiveToolNames?.ToList() ?? [];
    }

    public AgentPersona(
        AIAgent agent,
        PersonaConfig config,
        CollaborativeMarkdownDocument doc,
        RateLimiter? rateLimiter,
        int maxTurns,
        int agentCount,
        IRetryService retryService)
        : this(agent, config, doc, rateLimiter, maxTurns, agentCount, retryService, null)
    {
    }

    public AgentPersona(
        AIAgent agent,
        PersonaConfig config,
        CollaborativeMarkdownDocument doc,
        RateLimiter? rateLimiter,
        int maxTurns,
        int agentCount)
        : this(agent, config, doc, rateLimiter, maxTurns, agentCount, new RetryService(null))
    {
    }

    public async Task<string> RespondAsync(string currentMessage, List<string> conversationHistory, CancellationToken cancellationToken = default)
    {
        var contextMessage = BuildContext(currentMessage, conversationHistory);
        var telemetry = new RequestTelemetry(_eventSink, $"{Name} response", contextMessage);

        // Throttle based on an estimated token count
        var estimatedTokens = _rateLimiter?.EstimateTokens(contextMessage) ?? 0;
        if (_rateLimiter != null)
        {
            await _rateLimiter.ThrottleAsync(estimatedTokens, cancellationToken);
        }

        // Execute with retry logic for rate limiting (HTTP 429)
        string responseText;
        try
        {
            var response = await _retryService.ExecuteAsync(
                async cancellationToken => await _agent.RunAsync(
                    contextMessage, GetThread(), cancellationToken: cancellationToken),
                $"{Name} response",
                cancellationToken,
                _rateLimiter is null ? null : token => _rateLimiter.ThrottleAsync(0, token));
            responseText = response.ToString();
            telemetry.AppendOutput(responseText);
            telemetry.Complete(response.Usage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            telemetry.Fail(new TimeoutException("Operation timed out."));
            responseText = $"Error: Operation timed out.";
        }
        catch (HttpRequestException)
        {
            telemetry.Fail(new HttpRequestException("Network error occurred."));
            responseText = $"Error: Network error occurred.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            telemetry.Fail(ex);
            responseText = $"Error: An unexpected error occurred.";
        }

        // Account response tokens approximately
        _rateLimiter?.AccountAdditionalTokens(_rateLimiter.EstimateTokens(responseText));

        return responseText;
    }

    public async Task<string> AssessConsensusAsync(
        string currentMessage,
        List<string> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var contextMessage = BuildContext(
            $"""
            Assess whether the current document has reached consensus.
            Review the full Markdown document and recent discussion.
            The orchestrator's proposed completion message is:
            {currentMessage}
            Return exactly:
            CONSENSUS: YES or CONSENSUS: NO
            Reason: one concise sentence explaining your assessment.
            Use CONSENSUS: NO if your expertise identifies a material unresolved issue.
            """,
            conversationHistory);
        var telemetry = new RequestTelemetry(_eventSink, $"{Name} consensus check", contextMessage);

        try
        {
            var response = await _retryService.ExecuteAsync(
                async token => await _agent.RunAsync(
                    contextMessage, GetThread(), cancellationToken: token),
                $"{Name} consensus check",
                cancellationToken,
                _rateLimiter is null ? null : token => _rateLimiter.ThrottleAsync(0, token));
            var responseText = response.ToString();
            telemetry.AppendOutput(responseText);
            telemetry.Complete(response.Usage);
            return responseText;
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);
            throw;
        }
    }

    public async IAsyncEnumerable<string> RespondStreamingAsync(
        string currentMessage,
        List<string> conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var contextMessage = BuildContext(currentMessage, conversationHistory);
        var estimatedTokens = _rateLimiter?.EstimateTokens(contextMessage) ?? 0;
        if (_rateLimiter != null)
            await _rateLimiter.ThrottleAsync(estimatedTokens, cancellationToken);

        if (!_providerConfig.StreamingEnabled || !SupportsStreaming())
        {
            if (UseBufferedFallback())
            {
                await foreach (var chunk in FallbackToBufferedAsync(contextMessage, cancellationToken))
                    yield return chunk;
            }
            else
            {
                throw new NotSupportedException(
                    $"Streaming is not available for provider '{_providerConfig.Type}'.");
            }
            yield break;
        }

        var emitted = false;
        Exception? failure = null;
        var telemetry = new RequestTelemetry(_eventSink, $"{Name} streaming response", contextMessage);
        IAsyncEnumerator<AgentRunResponseUpdate>? updates = null;
        (IAsyncEnumerator<AgentRunResponseUpdate> enumerator, bool hasUpdate) initialUpdate;
        try
        {
            initialUpdate = await _retryService.ExecuteAsync(
                async token =>
                {
                    var enumerator = _agent
                        .RunStreamingAsync(contextMessage, GetThread(), cancellationToken: token)
                        .GetAsyncEnumerator(token);
                    try
                    {
                        return (enumerator: enumerator, hasUpdate: await enumerator.MoveNextAsync());
                    }
                    catch
                    {
                        await enumerator.DisposeAsync();
                        throw;
                    }
                },
                $"{Name} streaming response",
                cancellationToken,
                _rateLimiter is null ? null : token => _rateLimiter.ThrottleAsync(0, token));
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);
            throw;
        }
        updates = initialUpdate.enumerator;
        var hasUpdate = initialUpdate.hasUpdate;
        try
        {
            while (hasUpdate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var reasoning in updates.Current.Contents.OfType<TextReasoningContent>())
                    telemetry.PublishThinking(reasoning.Text);
                var text = updates.Current.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    emitted = true;
                    telemetry.AppendOutput(text);
                    _rateLimiter?.AccountAdditionalTokens(_rateLimiter.EstimateTokens(text));
                    yield return text;
                }

                try
                {
                    hasUpdate = await updates.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    failure = ex;
                    break;
                }
            }
        }
        finally
        {
            await updates.DisposeAsync();
        }

        if (failure is OperationCanceledException)
        {
            telemetry.Fail(failure);
            throw failure;
        }
        if (failure is not null && emitted)
        {
            telemetry.Fail(failure);
            throw failure;
        }
        if (failure is not null && UseBufferedFallback())
        {
            telemetry.Fail(failure);
            await foreach (var chunk in FallbackToBufferedAsync(contextMessage, cancellationToken))
                yield return chunk;
        }
        else if (failure is not null)
        {
            telemetry.Fail(failure);
            throw failure;
        }
        else
        {
            telemetry.Complete();
        }
    }

    private async IAsyncEnumerable<string> FallbackToBufferedAsync(
        string contextMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await _retryService.ExecuteAsync(
            async token => await _agent.RunAsync(contextMessage, GetThread(), cancellationToken: token),
            $"{Name} response",
            cancellationToken,
            _rateLimiter is null ? null : token => _rateLimiter.ThrottleAsync(0, token));
        var responseText = response.ToString();
        _rateLimiter?.AccountAdditionalTokens(_rateLimiter.EstimateTokens(responseText));
        yield return responseText;
    }

    private string BuildContext(string currentMessage, List<string> conversationHistory)
    {
        var recentHistory = conversationHistory.TakeLast(3).ToList();
        var contextMessage = recentHistory.Count > 0
            ? $"Recent conversation:\n{string.Join("\n", recentHistory)}\n\nCurrent message: {currentMessage}"
            : currentMessage;
        var docState = GetDocumentState();
        if (!string.IsNullOrWhiteSpace(docState))
            contextMessage = $"Current document state (use this exact Markdown as the source of truth):\n```markdown\n{docState}\n```\n\n{contextMessage}";

        var currentTurn = (conversationHistory.Count / _agentCount) + 1;
        var turnsRemaining = _maxTurns - currentTurn;
        if (turnsRemaining <= 2)
            contextMessage = $"⚠️ IMPORTANT: Only {turnsRemaining} turn(s) remaining. Focus on wrapping up and reaching a clear conclusion.\n\n{contextMessage}";

        return $"{GetPersonaCollaborationGuidelines()}\n\n{contextMessage}";
    }

    private AgentThread? GetThread()
    {
        if (_threadInitialized)
            return _thread;

        _threadInitialized = true;
        try
        {
            _thread = _agent.GetNewThread();
        }
        catch (NotSupportedException)
        {
            // Some test doubles and custom agents only support stateless runs.
        }

        return _thread;
    }

    private bool SupportsStreaming()
    {
        if (_providerConfig.StreamingSupported.HasValue)
            return _providerConfig.StreamingSupported.Value;

        return _providerConfig.Type.Equals("openai", StringComparison.OrdinalIgnoreCase) ||
               _providerConfig.Type.Equals("azureopenai", StringComparison.OrdinalIgnoreCase);
    }

    private bool UseBufferedFallback() =>
        _providerConfig.StreamingFallback.Equals("buffered", StringComparison.OrdinalIgnoreCase);

    private string GetPersonaCollaborationGuidelines()
    {
        var totalTurnsForAllPersonas = _maxTurns * _agentCount;
        return $@"You are collaborating with {_agentCount} persona(s) to produce ONE cohesive, CONCISE consensus document.
You have a maximum of {_maxTurns} rounds (total of {totalTurnsForAllPersonas} individual turns across all personas) to complete the work.

DELIVERABLE: A short markdown document that captures the agreed-upon stance.
Template:
# <Concise Title>
## Position
<1 short paragraph stating the agreed stance in plain language>
## Key Reasons
- <bullet 1>
- <bullet 2>
- <bullet 3>
## Trade-offs
- <bullet 1>
- <bullet 2>
## Final Recommendation
<1 short paragraph with the action-oriented recommendation>

CRITICAL GUIDELINES - CONCISENESS & CONSENSUS:
- BE CONCISE: Every sentence must serve a clear purpose. No rambling or verbose prose.
- AVOID NARRATIVE STYLE: This is a professional consensus statement, not an essay.
- SHORT PARAGRAPHS: Max 2-4 sentences. Use bullet points for lists.
- NO REDUNDANCY: Read what others have written. Don't repeat points already made.
- VISUALS: Use Mermaid.js diagrams (code blocks with 'mermaid') to visualize complex flows/structures.
- EDIT, DON'T JUST ADD: Use ReplaceSection to refine and consolidate content.
- PURPOSEFUL HEADINGS: Use only the template headings unless absolutely necessary.
- CONVERGE: If disagreement exists, capture the trade-off succinctly, then converge on a stance.

Completion Strategy:
- An editor will periodically review and refine the document for conciseness and coherence.
- As you approach the final rounds, prioritize convergence and finalize the recommendation.
- Avoid calling SaveToFile—the system auto-saves when the conversation finishes.";
    }

    private string GetDocumentState()
    {
        try
        {
            var content = _doc.Snapshot();
            return string.IsNullOrWhiteSpace(content) ? "Document is empty" : content;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            return string.Empty;
        }
    }

    public string GetDocumentPreview()
    {
        try
        {
            var content = _doc.Snapshot();
            return string.IsNullOrWhiteSpace(content) ? "  [Document is empty]" : content;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            return string.Empty;
        }
    }
}
