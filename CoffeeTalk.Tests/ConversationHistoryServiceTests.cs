using System.Text.Json;
using CoffeeTalk.Gui.Services;
using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class ConversationHistoryServiceTests
{
    [Fact]
    public async Task RecentMigratesLegacyHistoryAlongsideExistingConversations()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
        var resolver = new ApplicationDataPathResolver(root);
        var persistence = new ConversationPersistenceService(resolver);
        var legacyPath = resolver.ResolveDataPath("conversation-history.json", "conversation-history.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        try
        {
            await persistence.SaveAsync(new ConversationState
            {
                Id = "existing",
                Topic = "Existing",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(1)
            });
            await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(new[]
            {
                new ConversationRecord
                {
                    Id = "legacy",
                    Topic = "Legacy",
                    StartedAt = DateTime.UtcNow
                }
            }));

            var history = new ConversationHistoryService(resolver);

            var records = await history.RecentAsync();

            Assert.Equal(2, records.Count);
            Assert.Contains(records, record => record.Id == "legacy");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
