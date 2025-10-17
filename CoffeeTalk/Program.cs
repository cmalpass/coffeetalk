using Microsoft.Extensions.Configuration;
using CoffeeTalk.Models;
using CoffeeTalk.Services;

namespace CoffeeTalk;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("☕ Welcome to CoffeeTalk!");
        Console.WriteLine("A multi-persona LLM conversation orchestrator\n");

        try
        {
            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var settings = new AppSettings();
            configuration.Bind(settings);

            // Configure retry handler
            RetryHandler.Configure(settings.Retry);

            // Validate configuration
            if (string.IsNullOrWhiteSpace(settings.LlmProvider.ModelId))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: ModelId is not configured in appsettings.json");
                Console.ResetColor();
                return;
            }

            if (settings.Personas.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: No personas configured in appsettings.json");
                Console.ResetColor();
                return;
            }

            // Check for API key if using OpenAI
            switch (settings.LlmProvider.Type.ToLower())
            {
                case "openai":
                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.ApiKey))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("⚠️  Warning: API key not set. Checking environment variable OPENAI_API_KEY...");
                        Console.ResetColor();

                        var envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                        if (!string.IsNullOrWhiteSpace(envApiKey))
                        {
                            settings.LlmProvider.ApiKey = envApiKey;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Using API key from environment variable");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("❌ Error: OpenAI API key not found in config or environment");
                            Console.ResetColor();
                            return;
                        }
                    }
                    break;

                case "azureopenai":
                    // Azure OpenAI requires Endpoint, ApiKey, and DeploymentName (or ModelId as fallback)
                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.ApiKey))
                    {
                        var envApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
                        if (!string.IsNullOrWhiteSpace(envApiKey))
                        {
                            settings.LlmProvider.ApiKey = envApiKey;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Using Azure OpenAI API key from environment variable");
                            Console.ResetColor();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.Endpoint))
                    {
                        var envEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                        if (!string.IsNullOrWhiteSpace(envEndpoint))
                        {
                            settings.LlmProvider.Endpoint = envEndpoint;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Using Azure OpenAI endpoint from environment variable");
                            Console.ResetColor();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.DeploymentName))
                    {
                        var envDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME")
                            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
                        if (!string.IsNullOrWhiteSpace(envDeployment))
                        {
                            settings.LlmProvider.DeploymentName = envDeployment;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Using Azure OpenAI deployment name from environment variable");
                            Console.ResetColor();
                        }
                    }

                    // Final validation for required fields
                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.ApiKey) ||
                        string.IsNullOrWhiteSpace(settings.LlmProvider.Endpoint) ||
                        (string.IsNullOrWhiteSpace(settings.LlmProvider.DeploymentName) && string.IsNullOrWhiteSpace(settings.LlmProvider.ModelId)))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ Error: Azure OpenAI requires ApiKey, Endpoint, and DeploymentName (or ModelId used as DeploymentName)");
                        Console.ResetColor();
                        return;
                    }
                    break;

                case "ollama":
                    // Nothing to validate; local endpoint and no API key typically needed
                    break;
            }

            Console.WriteLine($"Provider: {settings.LlmProvider.Type}");
            Console.WriteLine($"Model: {settings.LlmProvider.ModelId}");

            // Get topic from user (needed for dynamic persona generation)
            Console.WriteLine("What would you like the personas to discuss?");
            Console.Write("Topic: ");
            var topic = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(topic))
            {
                Console.WriteLine("No topic provided. Exiting.");
                return;
            }

            // Build the kernel with the configured LLM provider
            var kernel = KernelBuilderService.BuildKernel(settings.LlmProvider);

            // Optionally generate personas dynamically
            if (settings.DynamicPersonas?.Enabled == true)
            {
                try
                {
                    var generator = new PersonaGenerator(kernel);
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
                        foreach (var p in generated) if (!map.ContainsKey(p.Name)) map[p.Name] = p;
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

                    Console.WriteLine($"Dynamic personas enabled ({settings.DynamicPersonas.Mode}). Using {settings.Personas.Count} persona(s): {string.Join(", ", settings.Personas.Select(p => p.Name))}\n");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠️  Dynamic persona generation failed: {ex.Message}. Proceeding with configured personas.");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine($"Personas: {string.Join(", ", settings.Personas.Select(p => p.Name))}\n");
            }

            // Verify tools and function-calling are available before starting
            var verifier = kernel.Services.GetService(typeof(CoffeeTalk.Services.ToolingVerifier)) as CoffeeTalk.Services.ToolingVerifier;
            if (verifier != null)
            {
                // Print available plugins and functions for visibility
                try
                {
                    var pluginNames = kernel.Plugins.Select(p => p.Name).ToList();
                    Console.WriteLine($"Available plugins: {string.Join(", ", pluginNames)}");
                }
                catch { }
                Console.WriteLine("\nRunning tool availability verification...");
                var ok = await verifier.VerifyAsync(kernel);
                if (!ok)
                {
                    var require = settings.Tools?.RequireToolsVerification ?? true;
                    Console.ForegroundColor = require ? ConsoleColor.Red : ConsoleColor.Yellow;
                    Console.WriteLine("❌ Tools verification failed: LLM could not invoke markdown tools. Please check provider, function calling support, and plugin registration.");
                    Console.ResetColor();
                    if (require) return;
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Tools verification passed");
                Console.ResetColor();
            }

            // Create orchestrator and start conversation
            var orchestrator = new ConversationOrchestrator(kernel, settings);
            await orchestrator.StartConversationAsync(topic);

            Console.WriteLine("\nThank you for using CoffeeTalk! ☕");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Fatal Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Console.ResetColor();
            Console.WriteLine("\nPlease check your configuration and try again.");
            Environment.Exit(1);
        }
    }
}
