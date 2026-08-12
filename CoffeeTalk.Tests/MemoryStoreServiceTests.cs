using CoffeeTalk.Gui.Services;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class MemoryStoreServiceTests
{
    [Fact]
    public async Task MemoryCrud_SearchAndPurge_AreWorkspaceScoped()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
        var resolver = new ApplicationDataPathResolver(root);
        var appState = new AppState(new ConfigurationService(resolver));
        appState.Settings.Memory.Enabled = true;
        var store = new MemoryStoreService(resolver, appState);

        var first = await store.AddAsync("Decision", "Use a local store for workspace notes.");
        await store.AddAsync("Other", "A separate fact.");

        Assert.Single(await store.SearchAsync("local"));
        Assert.Equal(2, (await store.GetStatusAsync()).MemoryCount);

        first.Content = "Use a local store and keep it opt-in.";
        await store.UpdateAsync(first);
        Assert.Contains("opt-in", (await store.SearchAsync("opt-in")).Single().Content);

        resolver.SwitchWorkspace("another");
        Assert.Empty(await store.SearchAsync());
        await store.AddAsync("Workspace note", "Only visible in another.");

        resolver.SwitchWorkspace(null);
        Assert.Equal(2, (await store.SearchAsync()).Count);
        await store.PurgeAsync();
        Assert.Empty(await store.SearchAsync());
    }

    [Fact]
    public async Task Settings_DefaultOff_AndPersistPerWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
        var resolver = new ApplicationDataPathResolver(root);
        var appState = new AppState(new ConfigurationService(resolver));
        var store = new MemoryStoreService(resolver, appState);

        Assert.False((await store.GetSettingsAsync()).Enabled);

        await store.SaveSettingsAsync(new MemorySettings { Enabled = true });
        Assert.True((await store.GetSettingsAsync()).Enabled);

        resolver.SwitchWorkspace("isolated");
        var isolatedState = new AppState(new ConfigurationService(resolver));
        var isolatedStore = new MemoryStoreService(resolver, isolatedState);
        Assert.False((await isolatedStore.GetSettingsAsync()).Enabled);
    }

    [Fact]
    public async Task CorruptCoreStore_IsNotSwallowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
        var resolver = new ApplicationDataPathResolver(root);
        var appState = new AppState(new ConfigurationService(resolver));
        var store = new MemoryStoreService(resolver, appState);
        var path = resolver.ResolveDataPath("memory/memory.json", "memory.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ invalid json");

        await Assert.ThrowsAsync<MemoryStoreCorruptException>(() => store.SearchAsync());
    }
}
