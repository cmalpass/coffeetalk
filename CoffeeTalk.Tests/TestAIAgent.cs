using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CoffeeTalk.Tests;

internal sealed class TestAIAgent : AIAgent
{
    private readonly Func<AgentRunResponse> _response;
    public int Calls { get; private set; }

    public TestAIAgent(Func<AgentRunResponse> response)
    {
        _response = response;
    }

    public TestAIAgent(string response)
        : this(() => new AgentRunResponse(new ChatMessage(ChatRole.Assistant, response)))
    {
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
        return Task.FromResult(_response());
    }

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}
