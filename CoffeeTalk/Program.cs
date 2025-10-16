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
            if (settings.LlmProvider.Type.ToLower() == "openai" && 
                string.IsNullOrWhiteSpace(settings.LlmProvider.ApiKey))
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

            Console.WriteLine($"Provider: {settings.LlmProvider.Type}");
            Console.WriteLine($"Model: {settings.LlmProvider.ModelId}");
            Console.WriteLine($"Personas: {string.Join(", ", settings.Personas.Select(p => p.Name))}\n");

            // Get topic from user
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
