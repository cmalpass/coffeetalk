using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;
using CoffeeTalk.Services;
using Microsoft.Agents.AI;
using Moq;
using Xunit;

namespace CoffeeTalk.Tests;

public sealed class ConversationPipelineBuilderTests
{
    [Fact]
    public async Task BuildAsync_AppliesEveryOptionalFeatureSetting()
    {
        var factory = new RecordingAgentFactory();
        var settings = CreateSettings();
        settings.DevilsAdvocate = true;
        settings.FactChecking = true;
        settings.Orchestrator!.Enabled = true;
        settings.Editor!.Enabled = true;
        settings.StructuredData!.Enabled = true;

        var pipeline = await CreateBuilder(factory).BuildAsync(settings, "topic");

        Assert.Equal(3, pipeline.Personas.Count);
        Assert.NotNull(pipeline.Orchestrator);
        Assert.NotNull(pipeline.Editor);
        Assert.NotNull(pipeline.FactChecker);
        Assert.NotNull(pipeline.DataExtractor);
        Assert.Equal(settings.Tools!.RequireToolsVerification, pipeline.ToolsConfig.RequireToolsVerification);
        Assert.Contains(factory.Created, agent => agent.Name == "Editor" && agent.Tools?.Length > 0);
        Assert.Contains(factory.Created, agent => agent.Name == "Analyst" && agent.Tools?.Length == 8);
    }

    [Fact]
    public async Task BuildAsync_UsesSameEffectivePipelineForCliAndGuiInputs()
    {
        var settings = CreateSettings();
        settings.DevilsAdvocate = true;
        settings.Orchestrator!.Enabled = true;
        settings.Tools = new ToolsConfig { EnableFallbackJsonTools = false, RequireToolsVerification = false };

        var cli = await CreateBuilder(new RecordingAgentFactory()).BuildAsync(settings, "topic");
        var gui = await CreateBuilder(new RecordingAgentFactory()).BuildAsync(
            settings,
            "topic",
            settings.Personas.ToList());

        Assert.Equal(
            new[] { "Analyst", "Designer", "DevilsAdvocate" },
            cli.Personas.Select(persona => persona.Name));
        Assert.Equal(
            cli.Personas.Select(persona => persona.Name),
            gui.Personas.Select(persona => persona.Name));
        Assert.Equal(cli.Orchestrator is not null, gui.Orchestrator is not null);
        Assert.Equal(cli.ToolsConfig.RequireToolsVerification, gui.ToolsConfig.RequireToolsVerification);
    }

    [Fact]
    public async Task BuildAsync_FiltersPersonaToolsByAllowedTools()
    {
        var factory = new RecordingAgentFactory();
        var settings = CreateSettings();
        settings.Personas[0].AllowedTools = ["AddHeading", "InsertAfterHeading"];

        var pipeline = await CreateBuilder(factory).BuildAsync(settings, "topic");

        var tools = factory.Created.Single(agent => agent.Name == "Analyst").Tools!;
        Assert.Equal(
            new[] { "AddHeading", "InsertAfterHeading" },
            tools.Select(tool => tool.Name));
        Assert.Equal(
            new[] { "AddHeading", "InsertAfterHeading" },
            pipeline.Personas.Single(persona => persona.Name == "Analyst").EffectiveToolNames);
    }

    [Fact]
    public async Task BuildAsync_RejectsUnknownPersonaTool()
    {
        var settings = CreateSettings();
        settings.Personas[0].AllowedTools = ["NotARealTool"];

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateBuilder(new RecordingAgentFactory()).BuildAsync(settings, "topic"));

        Assert.Contains("NotARealTool", exception.Message);
    }

    [Fact]
    public async Task BuildAsync_DoesNotExposeFallbackDispatcherToRestrictedPersona()
    {
        var factory = new RecordingAgentFactory();
        var settings = CreateSettings();
        settings.Personas[0].AllowedTools = ["SetTitle"];

        await CreateBuilder(factory).BuildAsync(settings, "topic");

        var tools = factory.Created.Single(agent => agent.Name == "Analyst").Tools!;
        Assert.DoesNotContain(tools, tool => tool.Name == "ExecuteJsonTool");
    }

    private static ConversationPipelineBuilder CreateBuilder(RecordingAgentFactory factory)
        => new(
            new ApplicationDataPathResolver(Path.Combine(Path.GetTempPath(), "coffeetalk-tests", Guid.NewGuid().ToString("N"))),
            agentFactory: factory);

    private static AppSettings CreateSettings()
        => new()
        {
            LlmProvider = new LlmProviderConfig
            {
                Type = "openai",
                ModelId = "test",
                ApiKey = "test"
            },
            Personas =
            [
                new PersonaConfig { Name = "Analyst", SystemPrompt = "You are Analyst." },
                new PersonaConfig { Name = "Designer", SystemPrompt = "You are Designer." }
            ],
            Tools = new ToolsConfig(),
            Orchestrator = new OrchestratorConfig(),
            Editor = new EditorConfig(),
            StructuredData = new StructuredDataConfig { SchemaDescription = "a record" }
        };

    private sealed class RecordingAgentFactory : IConversationAgentFactory
    {
        private readonly AIAgent _agent = new Mock<AIAgent>().Object;
        public List<CreatedAgent> Created { get; } = new();

        public AIAgent Create(LlmProviderConfig config, string name, string instructions, Microsoft.Extensions.AI.AIFunction[]? tools = null)
        {
            Created.Add(new CreatedAgent(name, tools));
            return _agent;
        }
    }

    private sealed record CreatedAgent(string Name, Microsoft.Extensions.AI.AIFunction[]? Tools);

}
