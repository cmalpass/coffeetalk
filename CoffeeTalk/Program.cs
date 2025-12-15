using Microsoft.Extensions.Configuration;
using CoffeeTalk.Models;
using CoffeeTalk.Services;
using Spectre.Console;

namespace CoffeeTalk;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var configService = new ConfigurationService();
            var settings = configService.LoadConfiguration();

            // Validate and interactively configure if needed
            settings = await configService.ValidateAndConfigureAsync(settings);

            // Configure retry handler
            RetryHandler.Configure(settings.Retry);

            // Display configuration summary
            AnsiConsole.MarkupLine($"[bold]Provider:[/] [cyan]{Markup.Escape(settings.LlmProvider.Type)}[/]");
            AnsiConsole.MarkupLine($"[bold]Model:[/] [cyan]{Markup.Escape(settings.LlmProvider.ModelId)}[/]");
            AnsiConsole.WriteLine();

            // Get topic from user (needed for dynamic persona generation)
            var topic = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold yellow]What would you like the personas to discuss?[/]")
                    .Validate(input =>
                    {
                        if (string.IsNullOrWhiteSpace(input))
                            return ValidationResult.Error("[red]Please enter a non-empty topic.[/]");
                        return ValidationResult.Success();
                    }));

            // Create shared collaborative document
            var sharedDoc = new CollaborativeMarkdownDocument();

            // Create markdown tool functions
            var markdownTools = new MarkdownToolFunctions(sharedDoc);
            var tools = markdownTools.CreateTools();

            // Optionally generate personas dynamically
            if (settings.DynamicPersonas?.Enabled == true)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Star)
                    .StartAsync("Generating dynamic personas...", async ctx =>
                    {
                        try
                        {
                            var generatorPrompt = AgentPersonaGenerator.BuildSystemPrompt();
                            var generatorAgent = AgentBuilder.CreateAgent(
                                settings.LlmProvider,
                                "PersonaGenerator",
                                generatorPrompt);
                            var generator = new AgentPersonaGenerator(generatorAgent);

                            var requested = Math.Clamp(settings.DynamicPersonas.Count, 2, 10);
                            var reserved = (settings.DynamicPersonas.Mode?.Equals("replace", StringComparison.OrdinalIgnoreCase) ?? false)
                                ? Array.Empty<string>()
                                : settings.Personas.Select(p => p.Name);

                            var generated = await generator.GenerateAsync(topic, requested, reserved);

                            List<PersonaConfig> finalList;
                            if (settings.DynamicPersonas.Mode?.Equals("replace", StringComparison.OrdinalIgnoreCase) ?? false)
                            {
                                finalList = generated;
                            }
                            else
                            {
                                // augment: merge without duplicate names (case-insensitive), preferring existing personas
                                var map = new Dictionary<string, PersonaConfig>(StringComparer.OrdinalIgnoreCase);
                                foreach (var p in settings.Personas) map[p.Name] = p;
                                foreach (var p in generated.Where(g => !map.ContainsKey(g.Name))) map[p.Name] = p;
                                finalList = map.Values.ToList();
                            }

                            // Enforce total personas to 2..10
                            if (finalList.Count < 2)
                            {
                                // top up with additional generated personas
                                var topup = await generator.GenerateAsync(topic, 2 - finalList.Count, finalList.Select(p => p.Name));
                                foreach (var p in topup) finalList.Add(p);
                            }
                            else if (finalList.Count > 10)
                            {
                                finalList = finalList.Take(10).ToList();
                            }

                            settings.Personas = finalList;

                            AnsiConsole.MarkupLine($"[green]✓ Dynamic personas enabled ({settings.DynamicPersonas.Mode}). Using {settings.Personas.Count} persona(s): {string.Join(", ", settings.Personas.Select(p => Markup.Escape(p.Name)))}[/]\n");
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[yellow]⚠️  Dynamic persona generation failed: {Markup.Escape(ex.Message)}. Proceeding with configured personas.[/]");
                        }
                    });
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold]Personas:[/] {string.Join(", ", settings.Personas.Select(p => Markup.Escape(p.Name)))}\n");
            }

            // Create persona agents
            var rateLimiter = new RateLimiter(settings.RateLimit);
            var agentPersonas = new List<AgentPersona>();
            
            foreach (var personaConfig in settings.Personas)
            {
                var agent = AgentBuilder.CreateAgent(
                    settings.LlmProvider,
                    personaConfig.Name,
                    personaConfig.SystemPrompt,
                    tools);
                
                var agentPersona = new AgentPersona(
                    agent,
                    personaConfig,
                    sharedDoc,
                    rateLimiter,
                    settings.MaxConversationTurns,
                    settings.Personas.Count);
                
                agentPersonas.Add(agentPersona);
            }

            // Inject Devil's Advocate if enabled
            if (settings.DevilsAdvocate)
            {
                var daConfig = new PersonaConfig
                {
                    Name = "DevilsAdvocate",
                    SystemPrompt = "You are the Devil's Advocate. Your sole purpose is to challenge assumptions, find flaws in logic, and force the team to strengthen their arguments. Be constructive but relentless. Do not agree just to be polite. If everyone agrees, find a reason why they might be wrong."
                };

                // Avoid duplicate if already in config
                if (!agentPersonas.Any(p => p.Name.Equals(daConfig.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var daAgent = AgentBuilder.CreateAgent(
                        settings.LlmProvider,
                        daConfig.Name,
                        daConfig.SystemPrompt,
                        tools);

                    var daPersona = new AgentPersona(
                        daAgent,
                        daConfig,
                        sharedDoc,
                        rateLimiter,
                        settings.MaxConversationTurns,
                        agentPersonas.Count + 1); // +1 because we are adding it now

                    agentPersonas.Add(daPersona);
                    AnsiConsole.MarkupLine("[magenta]😈 Devil's Advocate injected![/]");
                }
            }

            // Create orchestrator if enabled
            AgentOrchestrator? orchestrator = null;
            if (settings.Orchestrator?.Enabled ?? false)
            {
                var orchestratorConfig = settings.Orchestrator ?? new OrchestratorConfig();
                var orchestratorPrompt = AgentOrchestrator.BuildSystemPrompt(orchestratorConfig, agentPersonas);
                var orchestratorAgent = AgentBuilder.CreateAgent(
                    settings.LlmProvider,
                    "Orchestrator",
                    orchestratorPrompt);
                
                orchestrator = new AgentOrchestrator(
                    orchestratorAgent,
                    orchestratorConfig,
                    sharedDoc,
                    agentPersonas);
            }

            // Create editor if enabled
            AgentEditor? editor = null;
            if (settings.Editor?.Enabled ?? false)
            {
                var editorPrompt = AgentEditor.BuildSystemPrompt(settings.Editor);
                var editorAgent = AgentBuilder.CreateAgent(
                    settings.LlmProvider,
                    "Editor",
                    editorPrompt,
                    tools);
                
                editor = new AgentEditor(
                    editorAgent,
                    settings.Editor,
                    sharedDoc,
                    rateLimiter);
            }

            // Create Fact Checker if enabled
            AgentFactChecker? factChecker = null;
            if (settings.FactChecking)
            {
                var factPrompt = AgentFactChecker.BuildSystemPrompt();
                var factAgent = AgentBuilder.CreateAgent(settings.LlmProvider, "FactChecker", factPrompt);
                factChecker = new AgentFactChecker(factAgent, rateLimiter);
            }

            // Create conversation orchestrator and start conversation
            AgentDataExtractor? dataExtractor = null;
            if (settings.StructuredData?.Enabled == true)
            {
                var prompt = AgentDataExtractor.BuildSystemPrompt(settings.StructuredData);
                var agent = AgentBuilder.CreateAgent(settings.LlmProvider, "DataExtractor", prompt);
                dataExtractor = new AgentDataExtractor(agent, settings.StructuredData, sharedDoc);
            }

            var conversationOrchestrator = new AgentConversationOrchestrator(
                agentPersonas,
                sharedDoc,
                settings,
                orchestrator,
                editor,
                dataExtractor,
                factChecker);
            
            await conversationOrchestrator.StartConversationAsync(topic);

            AnsiConsole.MarkupLine("\n[bold green]Thank you for using CoffeeTalk! ☕[/]");
        }
        catch (OperationCanceledException ex)
        {
            AnsiConsole.MarkupLine($"\n[yellow]⚠️  Operation canceled: {Markup.Escape(ex.Message)}[/]");
            Environment.Exit(1);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine("\n[bold red]Please check your configuration and try again.[/]");
            Environment.Exit(1);
        }
    }
}
