using System.Text.Json;
using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class ConversationPersistenceServiceTests
{
    [Fact]
    public async Task SaveResumeRoundTripPreservesState()
    {
        var root = NewRoot();
        var service = new ConversationPersistenceService(new ApplicationDataPathResolver(root));
        var state = new ConversationState
        {
            Id = "round-trip",
            Topic = "Planning",
            DocumentContent = "# Notes",
            StartedAt = DateTimeOffset.UtcNow,
            Participants = new() { new() { Name = "Engineer", Role = "builder" } },
            Messages = new() { new() { Sender = "Engineer", Content = "Ready" } },
            Metadata = new() { ["source"] = "test" }
        };

        await service.SaveAsync(state);
        var restored = await service.ResumeAsync("round-trip");

        Assert.Equal(state.Topic, restored.Topic);
        Assert.Equal(state.DocumentContent, restored.DocumentContent);
        Assert.Equal("Engineer", restored.Participants[0].Name);
        Assert.Equal("test", restored.Metadata["source"]);
        Assert.Equal(1, restored.Metrics.MessageCount);
        Assert.Equal(1, restored.Metrics.WordCount);
    }

    [Fact]
    public async Task ResumeRejectsVersionMismatchAndCorruption()
    {
        var root = NewRoot();
        var resolver = new ApplicationDataPathResolver(root);
        var service = new ConversationPersistenceService(resolver);
        await service.SaveAsync(new ConversationState { Id = "versioned" });
        var path = resolver.ResolveDataPath("conversations/versioned.json", "conversation.json");
        var json = await File.ReadAllTextAsync(path);
        var node = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
        node["schemaVersion"] = 99;
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(node));
        await Assert.ThrowsAsync<ConversationStateVersionException>(() => service.ResumeAsync("versioned"));
        await File.WriteAllTextAsync(path, "{");
        await Assert.ThrowsAsync<ConversationStateCorruptException>(() => service.ResumeAsync("versioned"));
    }

    [Fact]
    public async Task PathsAndCancellationAreEnforced()
    {
        var service = new ConversationPersistenceService(new ApplicationDataPathResolver(NewRoot()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResumeAsync("../escape"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync(new ConversationState(), cancellation.Token));
    }

    [Fact]
    public async Task ListAndDeleteManageSavedStates()
    {
        var service = new ConversationPersistenceService(new ApplicationDataPathResolver(NewRoot()));
        await service.SaveAsync(new ConversationState { Id = "one", Topic = "One" });
        await service.SaveAsync(new ConversationState { Id = "two", Topic = "Two" });

        Assert.Equal(2, (await service.ListAsync()).Count);
        await service.DeleteAsync("one");
        Assert.Single(await service.ListAsync());
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.ResumeAsync("one"));
    }

    [Fact]
    public async Task ResumeCalculatesMetricsForOlderStatesWithoutMetrics()
    {
        var root = NewRoot();
        var resolver = new ApplicationDataPathResolver(root);
        var service = new ConversationPersistenceService(resolver);
        var path = resolver.ResolveDataPath("conversations/legacy.json", "conversation.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
            {
              "schemaVersion": 1,
              "id": "legacy",
              "topic": "Legacy",
              "startedAt": "2026-01-01T00:00:00Z",
              "messages": [
                { "sender": "Alice", "content": "Hello there", "timestamp": "2026-01-01T00:00:01Z" }
              ],
              "participants": [],
              "metadata": {}
            }
            """);

        var restored = await service.ResumeAsync("legacy");

        Assert.Equal(1, restored.Metrics.MessageCount);
        Assert.Equal(2, restored.Metrics.WordCount);
    }

    [Fact]
    public async Task ResumeRejectsSymlinkedConversationFile()
    {
        var root = NewRoot();
        var resolver = new ApplicationDataPathResolver(root);
        var service = new ConversationPersistenceService(resolver);
        var outside = Path.Combine(Path.GetTempPath(), $"coffeetalk-outside-{Guid.NewGuid():N}.json");
        var link = resolver.ResolveDataPath("conversations/linked.json", "conversation.json");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        try
        {
            await File.WriteAllTextAsync(outside, "{}");
            File.CreateSymbolicLink(link, outside);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResumeAsync("linked"));
        }
        finally
        {
            if (File.Exists(link))
                File.Delete(link);
            if (File.Exists(outside))
                File.Delete(outside);
        }
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
}
