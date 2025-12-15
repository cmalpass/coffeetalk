using CoffeeTalk.Models;
using CoffeeTalk.Services;
using Spectre.Console;

namespace CoffeeTalk.Helpers;

public static class ConfigurationHelper
{
    public static async Task<AppSettings> ValidateAndConfigureAsync(ConfigurationService configService, AppSettings settings)
    {
        bool needsSave = false;

        // Header
        AnsiConsole.Write(
            new FigletText("CoffeeTalk")
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[bold blue]A multi-persona LLM conversation orchestrator[/]");
        AnsiConsole.MarkupLine("[bold blue]Powered by Microsoft Agent Framework[/]\n");

        // Provider Configuration
        if (string.IsNullOrWhiteSpace(settings.LlmProvider.ModelId) ||
            string.IsNullOrWhiteSpace(settings.LlmProvider.Type))
        {
            if (AnsiConsole.Confirm("Configuration is missing or incomplete. Would you like to run the setup wizard?", true))
            {
                await RunSetupWizardAsync(settings);
                needsSave = true;
            }
            else
            {
                throw new InvalidOperationException("Configuration incomplete and setup wizard declined.");
            }
        }
        else
        {
            // Check for API key if using OpenAI
            switch (settings.LlmProvider.Type.ToLower())
            {
                case "openai":
                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.ApiKey))
                    {
                        var envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                        if (!string.IsNullOrWhiteSpace(envApiKey))
                        {
                            settings.LlmProvider.ApiKey = envApiKey;
                            AnsiConsole.MarkupLine("[green]✓ Using API key from environment variable[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[yellow]⚠️  API key not found.[/]");
                            AnsiConsole.MarkupLine("[yellow]Your API key will NOT be saved to disk and must be provided again in future sessions.[/]");
                            settings.LlmProvider.ApiKey = AnsiConsole.Prompt(
                                new TextPrompt<string>("Please enter your OpenAI API Key:")
                                    .Secret());
                        }
                    }
                    break;
                case "azureopenai":
                    // Similar logic for Azure
                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.ApiKey))
                    {
                        var envApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
                        if (!string.IsNullOrWhiteSpace(envApiKey))
                        {
                            settings.LlmProvider.ApiKey = envApiKey;
                            AnsiConsole.MarkupLine("[green]✓ Using Azure OpenAI API key from environment variable[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[yellow]⚠️  Azure OpenAI API key not found.[/]");
                            AnsiConsole.MarkupLine("[yellow]Your API key will NOT be saved to disk and must be provided again in future sessions.[/]");
                            settings.LlmProvider.ApiKey = AnsiConsole.Prompt(
                                new TextPrompt<string>("Please enter your Azure OpenAI API Key:")
                                    .Secret());
                        }
                    }
                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.Endpoint))
                    {
                        var envEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                        if (!string.IsNullOrWhiteSpace(envEndpoint))
                        {
                            settings.LlmProvider.Endpoint = envEndpoint;
                            AnsiConsole.MarkupLine("[green]✓ Using Azure OpenAI endpoint from environment variable[/]");
                        }
                        else
                        {
                            settings.LlmProvider.Endpoint = AnsiConsole.Ask<string>("Please enter your Azure OpenAI Endpoint:");
                            needsSave = true;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(settings.LlmProvider.DeploymentName))
                    {
                        var envDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME")
                            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
                        if (!string.IsNullOrWhiteSpace(envDeployment))
                        {
                            settings.LlmProvider.DeploymentName = envDeployment;
                            AnsiConsole.MarkupLine("[green]✓ Using Azure OpenAI deployment name from environment variable[/]");
                        }
                        else
                        {
                            settings.LlmProvider.DeploymentName = AnsiConsole.Ask<string>("Please enter your Azure OpenAI Deployment Name:");
                            needsSave = true;
                        }
                    }
                    break;
            }
        }

        if (needsSave && AnsiConsole.Confirm("Would you like to save these settings to appsettings.json?", true))
        {
            await configService.SaveSettingsAsync(settings);
            AnsiConsole.MarkupLine("[green]✓ Settings saved to appsettings.json[/]");
        }

        return settings;
    }

    private static Task RunSetupWizardAsync(AppSettings settings)
    {
        var provider = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select your LLM Provider:")
                .AddChoices(new[] { "openai", "azureopenai", "ollama" }));

        settings.LlmProvider.Type = provider;

        switch (provider)
        {
            case "openai":
                settings.LlmProvider.ApiKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter your [bold]OpenAI API Key[/]:").Secret());
                settings.LlmProvider.ModelId = AnsiConsole.Ask<string>("Enter the [bold]Model ID[/] (e.g., gpt-4o-mini):", "gpt-4o-mini");
                settings.LlmProvider.Endpoint = "https://api.openai.com/v1";
                break;

            case "azureopenai":
                settings.LlmProvider.ApiKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter your [bold]Azure OpenAI API Key[/]:").Secret());
                settings.LlmProvider.Endpoint = AnsiConsole.Ask<string>("Enter your [bold]Azure OpenAI Endpoint[/]:");
                settings.LlmProvider.DeploymentName = AnsiConsole.Ask<string>("Enter your [bold]Deployment Name[/]:");
                settings.LlmProvider.ModelId = settings.LlmProvider.DeploymentName; // Usually same
                break;

            case "ollama":
                settings.LlmProvider.Endpoint = AnsiConsole.Ask<string>("Enter your [bold]Ollama Endpoint[/]:", "http://localhost:11434");
                settings.LlmProvider.ModelId = AnsiConsole.Ask<string>("Enter the [bold]Model Name[/] (e.g., llama2):", "llama2");
                break;
        }

        settings.InteractiveMode = AnsiConsole.Confirm("Enable [bold]Interactive Mode[/] (Director's Chair)?", false);
        settings.DevilsAdvocate = AnsiConsole.Confirm("Enable [bold]Devil's Advocate[/] mode?", false);
        settings.ContextSummarization = AnsiConsole.Confirm("Enable [bold]Context Summarization[/]?", false);
        settings.FactChecking = AnsiConsole.Confirm("Enable [bold]Fact Checking Agent[/]?", false);

        if (AnsiConsole.Confirm("Enable [bold]Structured Data Extraction[/] (JSON)?", false))
        {
            settings.StructuredData = new StructuredDataConfig
            {
                Enabled = true,
                SchemaDescription = AnsiConsole.Ask<string>("Enter schema description (e.g., 'Extract action items'):", "Extract key action items and decisions.")
            };
        }

        if (settings.Editor?.Enabled == true)
        {
             if (AnsiConsole.Confirm("Do you want to add specific [bold]Style Guidelines[/] (e.g., 'Use legal terminology')?", false))
             {
                 settings.Editor.StyleGuidelines = AnsiConsole.Ask<string>("Enter your Style Guidelines:");
             }
        }

        if (settings.Personas.Count == 0 && AnsiConsole.Confirm("No personas configured. Add default personas?", true))
        {
            settings.Personas.Add(new PersonaConfig
            {
                Name = "ProductManager",
                SystemPrompt = "You are a product manager focused on user value, market fit, and strategic priorities. Keep responses user-focused and concise."
            });
            settings.Personas.Add(new PersonaConfig
            {
                Name = "Engineer",
                SystemPrompt = "You are a software engineer focused on technical feasibility, architecture, and implementation. Keep responses technical and concise."
            });
        }

        return Task.CompletedTask;
    }
}
