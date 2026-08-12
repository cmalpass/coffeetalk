using System.Text.Json;
using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task WorkspacesIsolateConfigurationAndConversationState()
    {
        var root = NewRoot();
        var paths = new ApplicationDataPathResolver(root);
        var workspaces = new WorkspaceService(paths);
        var first = await workspaces.CreateAsync("Project Alpha");
        await workspaces.SwitchAsync(first.Id);
        await new ConfigurationService(paths).SaveSettingsAsync(new AppSettings { Personas = [new PersonaConfig { Name = "Alpha" }] });
        await new ConversationPersistenceService(paths).SaveAsync(new ConversationState { Id = "alpha", Topic = "Alpha" });

        var second = await workspaces.CreateAsync("Project Beta");
        await workspaces.SwitchAsync(second.Id);

        Assert.Empty(await new ConversationPersistenceService(paths).ListAsync());
        Assert.Empty(new ConfigurationService(paths).LoadConfiguration().Personas);
        Assert.Equal(second.Id, workspaces.Active.Id);
        Assert.True(File.Exists(Path.Combine(root, "workspaces", first.Id, "conversations", "alpha.json")));
    }

    [Fact]
    public async Task MigratesStandaloneFilesIntoDefaultWorkspace()
    {
        var root = NewRoot();
        Directory.CreateDirectory(Path.Combine(root, "conversations"));
        await File.WriteAllTextAsync(Path.Combine(root, "appsettings.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(root, "conversations", "old.json"), "{}");

        var workspaces = new WorkspaceService(new ApplicationDataPathResolver(root));

        Assert.Equal("default", workspaces.Active.Id);
        Assert.True(File.Exists(Path.Combine(root, "workspaces", "default", "appsettings.json")));
        Assert.True(File.Exists(Path.Combine(root, "workspaces", "default", "conversations", "old.json")));
        Assert.False(File.Exists(Path.Combine(root, "appsettings.json")));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("folder/name")]
    [InlineData("..")]
    public void WorkspaceIdentifiersRejectTraversal(string id)
    {
        Assert.Throws<UnauthorizedAccessException>(() => WorkspaceNameValidator.Validate(id));
    }

    [Fact]
    public async Task CorruptMetadataIsIgnoredAndDoesNotBecomeActive()
    {
        var root = NewRoot();
        var workspaceRoot = Path.Combine(root, "workspaces", "broken");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "workspace.json"), "{");

        var service = new WorkspaceService(new ApplicationDataPathResolver(root));

        Assert.Equal("default", service.Active.Id);
        Assert.DoesNotContain((await service.ListAsync()), item => item.Id == "broken");
        using var active = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "active-workspace.json")));
        Assert.Equal("default", active.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task DeletingActiveWorkspaceSwitchesToAnotherWorkspace()
    {
        var service = new WorkspaceService(new ApplicationDataPathResolver(NewRoot()));
        var created = await service.CreateAsync("Temporary");
        await service.SwitchAsync(created.Id);

        await service.DeleteAsync(created.Id);

        Assert.Equal("default", service.Active.Id);
        Assert.DoesNotContain((await service.ListAsync()), item => item.Id == created.Id);
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"));
}
