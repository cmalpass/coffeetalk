using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CoffeeTalk.Tests;

internal sealed class TestAIAgent : AIAgent
{
    private readonly Func<AgentRunResponse> _response;
    private readonly IReadOnlyList<string> _streamingChunks;
    private readonly Exception? _streamingException;
    private readonly bool _failBeforeStreamingOutput;
    public int Calls { get; private set; }
    public int StreamingCalls { get; private set; }
    public List<string> Prompts { get; } = [];

    public TestAIAgent(Func<AgentRunResponse> response)
    {
        _response = response;
        _streamingChunks = [];
    }

    public TestAIAgent(string response)
        : this(() => new AgentRunResponse(new ChatMessage(ChatRole.Assistant, response)))
    {
    }

    public TestAIAgent(
        string response,
        IReadOnlyList<string> streamingChunks,
        Exception? streamingException = null,
        bool failBeforeStreamingOutput = false)
        : this(() => new AgentRunResponse(new ChatMessage(ChatRole.Assistant, response)))
    {
        _streamingChunks = streamingChunks;
        _streamingException = streamingException;
        _failBeforeStreamingOutput = failBeforeStreamingOutput;
    }

    public TestAIAgent(Exception exception)
        : this(() => throw exception)
    {
    }

    public override AgentThread GetNewThread() => throw new NotSupportedException();

    public override AgentThread DeserializeThread(
        JsonElement serializedThread,
        System.Text.Json.JsonSerializerOptions? jsonSerializerOptions = null)
        => throw new NotSupportedException();

    public override Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        Prompts.Add(string.Join("\n", messages.Select(message => message.Text)));
        return Task.FromResult(_response());
    }

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamingCalls++;
        Prompts.Add(string.Join("\n", messages.Select(message => message.Text)));
        if (_failBeforeStreamingOutput && _streamingException is not null)
            throw _streamingException;

        foreach (var chunk in _streamingChunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new AgentRunResponseUpdate(new ChatResponseUpdate(ChatRole.Assistant, chunk));
            await Task.Yield();
        }

        if (_streamingChunks.Count == 0)
        {
            var response = _response();
            yield return new AgentRunResponseUpdate(new ChatResponseUpdate(ChatRole.Assistant, response.ToString()));
        }

        if (_streamingException is not null)
            throw _streamingException;
    }
}
