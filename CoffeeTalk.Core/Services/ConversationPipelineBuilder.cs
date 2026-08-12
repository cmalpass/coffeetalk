using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;

namespace CoffeeTalk.Services;

public interface IConversationAgentFactory
{
    AIAgent Create(LlmProviderConfig config, string name, string instructions, AIFunction[]? tools = null);
}

public sealed class ConversationAgentFactory : IConversationAgentFactory
{
    public AIAgent Create(LlmProviderConfig config, string name, string instructions, AIFunction[]? tools = null)
        => AgentBuilder.CreateAgent(config, name, instructions, tools);
}

public sealed class ConversationPipeline
{
    internal ConversationPipeline(
        CollaborativeMarkdownDocument sharedDocument,
        ToolsConfig toolsConfig,
        List<AgentPersona> personas,
        AgentOrchestrator? orchestrator,
        AgentEditor? editor,
        AgentDataExtractor? dataExtractor,
        AgentFactChecker? factChecker,
        AppSettings settings)
    {
        SharedDocument = sharedDocument;
        ToolsConfig = toolsConfig;
        Personas = personas;
        Orchestrator = orchestrator;
        Editor = editor;
        DataExtractor = dataExtractor;
        FactChecker = factChecker;
        Settings = settings;
    }

    public CollaborativeMarkdownDocument SharedDocument { get; }
    public ToolsConfig ToolsConfig { get; }
    public List<AgentPersona> Personas { get; }
    public AgentOrchestrator? Orchestrator { get; }
    public AgentEditor? Editor { get; }
    public AgentDataExtractor? DataExtractor { get; }
    public AgentFactChecker? FactChecker { get; }
    public AppSettings Settings { get; }

    public AgentConversationOrchestrator CreateConversation(IUserInterface ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        if (FactChecker != null)
        {
            FactChecker.OnFactCheckAlert += alert =>
                ui.ShowMessageAsync($"[bold red]Fact Checker Alert:[/] {alert}");
        }

        return new(ui, Personas, SharedDocument, Settings, Orchestrator, Editor, DataExtractor, FactChecker);
    }
}

public sealed class ConversationPipelineBuilder
{
    private const string DevilsAdvocateName = "DevilsAdvocate";
    private const string FallbackJsonToolName = "ExecuteJsonTool";
    private readonly IApplicationDataPathResolver _dataPaths;
    private readonly IOperationalEventSink _eventSink;
    private readonly IConversationAgentFactory _agentFactory;

    public ConversationPipelineBuilder(
        IApplicationDataPathResolver dataPaths,
        IOperationalEventSink? eventSink = null,
        IConversationAgentFactory? agentFactory = null)
    {
        _dataPaths = dataPaths ?? throw new ArgumentNullException(nameof(dataPaths));
        _eventSink = eventSink ?? NullOperationalEventSink.Instance;
        _agentFactory = agentFactory ?? new ConversationAgentFactory();
    }

    public async Task<ConversationPipeline> BuildAsync(
        AppSettings settings,
        string topic,
        IEnumerable<PersonaConfig>? selectedPersonas = null,
        CancellationToken cancellationToken = default,
        Action<string>? notify = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("A conversation topic is required.", nameof(topic));

        var sharedDocument = new CollaborativeMarkdownDocument(_dataPaths);
        var toolsConfig = settings.Tools ?? new ToolsConfig();
        var markdownTools = new MarkdownToolFunctions(sharedDocument, toolsConfig);
        var tools = markdownTools.CreateTools();
        if (toolsConfig.RequireToolsVerification && !markdownTools.VerifyTools(tools))
        {
            throw new InvalidOperationException("Markdown tools failed verification.");
        }
        var retryService = new RetryService(settings.Retry, _eventSink);
        var rateLimiter = new RateLimiter(settings.RateLimit);
        var personaConfigs = (selectedPersonas ?? settings.Personas).ToList();

        if (settings.DynamicPersonas?.Enabled == true)
        {
            personaConfigs = await GeneratePersonasAsync(
                settings,
                topic,
                personaConfigs,
                retryService,
                cancellationToken,
                notify);
        }

        ValidateAllowedTools(personaConfigs, tools);
        var personas = CreatePersonas(settings, personaConfigs, sharedDocument, rateLimiter, retryService, tools);

        if (settings.DevilsAdvocate &&
            !personas.Any(persona => persona.Name.Equals(DevilsAdvocateName, StringComparison.OrdinalIgnoreCase)))
        {
            var config = new PersonaConfig
            {
                Name = DevilsAdvocateName,
                SystemPrompt = "You are the Devil's Advocate. Your sole purpose is to challenge assumptions, find flaws in logic, and force the team to strengthen their arguments. Be constructive but relentless. Do not agree just to be polite. If everyone agrees, find a reason why they might be wrong."
            };
            personas.Add(CreatePersona(settings, config, sharedDocument, rateLimiter, retryService, personas.Count + 1, tools));
            notify?.Invoke("Devil's Advocate injected.");
        }

        AgentOrchestrator? orchestrator = null;
        if (settings.Orchestrator?.Enabled == true)
        {
            var config = settings.Orchestrator;
            var agent = _agentFactory.Create(
                settings.LlmProvider,
                "Orchestrator",
                AgentOrchestrator.BuildSystemPrompt(config, personas));
            orchestrator = new AgentOrchestrator(agent, config, sharedDocument, personas, retryService, _eventSink);
        }

        AgentEditor? editor = null;
        if (settings.Editor?.Enabled == true)
        {
            var config = settings.Editor;
            var agent = _agentFactory.Create(
                settings.LlmProvider,
                "Editor",
                AgentEditor.BuildSystemPrompt(config),
                tools);
            editor = new AgentEditor(agent, config, sharedDocument, rateLimiter, retryService);
        }

        AgentFactChecker? factChecker = null;
        if (settings.FactChecking)
        {
            var agent = _agentFactory.Create(
                settings.LlmProvider,
                "FactChecker",
                AgentFactChecker.BuildSystemPrompt());
            factChecker = new AgentFactChecker(agent, rateLimiter, retryService, _eventSink);
        }

        AgentDataExtractor? dataExtractor = null;
        if (settings.StructuredData?.Enabled == true)
        {
            var config = settings.StructuredData;
            var agent = _agentFactory.Create(
                settings.LlmProvider,
                "DataExtractor",
                AgentDataExtractor.BuildSystemPrompt(config));
            dataExtractor = new AgentDataExtractor(agent, config, sharedDocument, retryService, _eventSink, _dataPaths);
        }

        return new ConversationPipeline(
            sharedDocument,
            toolsConfig,
            personas,
            orchestrator,
            editor,
            dataExtractor,
            factChecker,
            settings);
    }

    private async Task<List<PersonaConfig>> GeneratePersonasAsync(
        AppSettings settings,
        string topic,
        List<PersonaConfig> configured,
        IRetryService retryService,
        CancellationToken cancellationToken,
        Action<string>? notify)
    {
        var dynamicConfig = settings.DynamicPersonas!;
        var generatorAgent = _agentFactory.Create(
            settings.LlmProvider,
            "PersonaGenerator",
            AgentPersonaGenerator.BuildSystemPrompt());
        var generator = new AgentPersonaGenerator(generatorAgent, retryService);
        var requested = Math.Clamp(dynamicConfig.Count, 2, 10);
        var replace = dynamicConfig.Mode?.Equals("replace", StringComparison.OrdinalIgnoreCase) == true;
        var reserved = replace ? Array.Empty<string>() : configured.Select(persona => persona.Name);

        try
        {
            var generated = await generator.GenerateAsync(topic, requested, reserved, cancellationToken);
            var result = replace
                ? generated
                : MergePersonas(configured, generated);

            if (result.Count < 2)
            {
                var topUp = await generator.GenerateAsync(
                    topic,
                    2 - result.Count,
                    result.Select(persona => persona.Name),
                    cancellationToken);
                result.AddRange(topUp);
            }

            notify?.Invoke($"Dynamic personas enabled ({dynamicConfig.Mode}); using {result.Count} persona(s).");
            return result.Take(10).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            notify?.Invoke($"Dynamic persona generation failed: {ex.Message}. Using configured personas.");
            return configured;
        }
    }

    private static List<PersonaConfig> MergePersonas(
        IEnumerable<PersonaConfig> configured,
        IEnumerable<PersonaConfig> generated)
    {
        var result = new List<PersonaConfig>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var persona in configured.Concat(generated))
        {
            if (names.Add(persona.Name))
                result.Add(persona);
        }
        return result;
    }

    private List<AgentPersona> CreatePersonas(
        AppSettings settings,
        IEnumerable<PersonaConfig> configs,
        CollaborativeMarkdownDocument sharedDocument,
        RateLimiter rateLimiter,
        IRetryService retryService,
        AIFunction[] tools)
    {
        var personaConfigs = configs.ToList();
        return personaConfigs
            .Select((config, index) => CreatePersona(
                settings,
                config,
                sharedDocument,
                rateLimiter,
                retryService,
                index + 1,
                tools,
                personaConfigs.Count))
            .ToList();
    }

    private AgentPersona CreatePersona(
        AppSettings settings,
        PersonaConfig config,
        CollaborativeMarkdownDocument sharedDocument,
        RateLimiter rateLimiter,
        IRetryService retryService,
        int agentCount,
        AIFunction[] tools,
        int? totalPersonaCount = null)
    {
        var personaTools = FilterTools(config, tools);
        var agent = _agentFactory.Create(settings.LlmProvider, config.Name, config.SystemPrompt, personaTools);
        return new AgentPersona(
            agent,
            config,
            sharedDocument,
            rateLimiter,
            settings.MaxConversationTurns,
            totalPersonaCount ?? agentCount,
            retryService,
            personaTools.Select(tool => tool.Name).ToList());
    }

    private static void ValidateAllowedTools(IEnumerable<PersonaConfig> configs, AIFunction[] tools)
    {
        var availableNames = tools
            .Select(tool => tool.Name)
            .Where(name => !name.Equals(FallbackJsonToolName, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var config in configs)
        {
            if (config.AllowedTools is null)
                continue;

            var unknown = config.AllowedTools
                .Where(name => !availableNames.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (unknown.Count > 0)
            {
                throw new ArgumentException(
                    $"Persona '{config.Name}' contains unknown or unsupported tools: {string.Join(", ", unknown)}.",
                    nameof(configs));
            }
        }
    }

    private static AIFunction[] FilterTools(PersonaConfig config, AIFunction[] tools)
    {
        if (config.AllowedTools is null)
            return tools;

        var allowed = config.AllowedTools.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tools
            .Where(tool =>
                !tool.Name.Equals(FallbackJsonToolName, StringComparison.OrdinalIgnoreCase) &&
                allowed.Contains(tool.Name))
            .ToArray();
    }
}
