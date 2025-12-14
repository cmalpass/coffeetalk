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

            // Read template file if configured
            string? initialContent = null;
            if (!string.IsNullOrWhiteSpace(settings.TemplateFile))
            {
                if (File.Exists(settings.TemplateFile))
                {
                    initialContent = await File.ReadAllTextAsync(settings.TemplateFile);
                    AnsiConsole.MarkupLine($"[green]✓ Loaded template from {Markup.Escape(settings.TemplateFile)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠️  Template file not found: {Markup.Escape(settings.TemplateFile)}[/]");
                }
            }

            // Read context files if configured
            var contextContent = new System.Text.StringBuilder();
            if (settings.ContextFiles.Count > 0)
            {
                foreach (var file in settings.ContextFiles)
                {
                    if (File.Exists(file))
                    {
                        try
                        {
                            var content = await File.ReadAllTextAsync(file);
                            contextContent.AppendLine($"\n--- Context from file: {Path.GetFileName(file)} ---");
                            contextContent.AppendLine(content);
                            contextContent.AppendLine("--- End of context ---");
                            AnsiConsole.MarkupLine($"[green]✓ Loaded context from {Markup.Escape(file)}[/]");
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            AnsiConsole.MarkupLine($"[yellow]⚠️  Access denied when reading context file {Markup.Escape(file)}: {Markup.Escape(ex.Message)}[/]");
                        }
                        catch (IOException ex)
                        {
                            AnsiConsole.MarkupLine($"[yellow]⚠️  IO error when reading context file {Markup.Escape(file)}: {Markup.Escape(ex.Message)}[/]");
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[yellow]⚠️  Unexpected error when reading context file {Markup.Escape(file)}: {Markup.Escape(ex.Message)}[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]⚠️  Context file not found: {Markup.Escape(file)}[/]");
                    }
                }
            }

            // Get topic from user (needed for dynamic persona generation)
            var topic = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold yellow]What would you like the personas to discuss?[/]")
                    .Validate(input =>
                    {
                        if (string.IsNullOrWhiteSpace(input))
                            return ValidationResult.Error("[red]Please enter a non-empty topic.[/]");
                        return ValidationResult.Success();
                    }));

            // Append context to topic for initial awareness
            if (contextContent.Length > 0)
            {
                // Validate context size (warn if exceeds 50KB)
                const int maxContextSize = 50 * 1024; // 50KB
                if (contextContent.Length > maxContextSize)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠️  Warning: Context files total {contextContent.Length} characters (>{maxContextSize}). This may exceed token limits.[/]");
                }
                
                // StringBuilder will be implicitly converted to string in concatenation
                topic += "\n\nContext information:\n" + contextContent;
            }

            // Create shared collaborative document
            var sharedDoc = new CollaborativeMarkdownDocument(initialContent);

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
            
            // Prepare language instruction if configured
            var langInstruction = !string.IsNullOrWhiteSpace(settings.OutputLanguage)
                ? $" You must converse and write the document in {settings.OutputLanguage}."
                : string.Empty;

            foreach (var personaConfig in settings.Personas)
            {
                // Apply language instruction without mutating the original config
                var systemPrompt = personaConfig.SystemPrompt + langInstruction;
                
                var agent = AgentBuilder.CreateAgent(
                    settings.LlmProvider,
                    personaConfig.Name,
                    systemPrompt,
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

            // Create orchestrator if enabled
            AgentOrchestrator? orchestrator = null;
            if (settings.Orchestrator?.Enabled ?? false)
            {
                var orchestratorConfig = settings.Orchestrator ?? new OrchestratorConfig();
                var orchestratorPrompt = AgentOrchestrator.BuildSystemPrompt(orchestratorConfig, agentPersonas);
                
                // Apply language instruction without mutating the original config
                orchestratorPrompt += langInstruction;
                
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

            // Create conversation orchestrator and start conversation
            var conversationOrchestrator = new AgentConversationOrchestrator(
                agentPersonas,
                sharedDoc,
                settings,
                orchestrator,
                editor);
            
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
