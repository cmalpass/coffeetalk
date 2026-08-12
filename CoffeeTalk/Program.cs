using Microsoft.Extensions.Configuration;
using CoffeeTalk.Models;
using CoffeeTalk.Services;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Helpers;
using Spectre.Console;

namespace CoffeeTalk;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var dataPaths = new ApplicationDataPathResolver();
            var configService = new ConfigurationService(dataPaths);
            var settings = configService.LoadConfiguration();
            settings = await ConfigurationHelper.ValidateAndConfigureAsync(configService, settings);

            var eventSink = new CliOperationalEventSink();
            AnsiConsole.MarkupLine($"[bold]Provider:[/] [cyan]{Markup.Escape(settings.LlmProvider.Type)}[/]");
            AnsiConsole.MarkupLine($"[bold]Model:[/] [cyan]{Markup.Escape(settings.LlmProvider.ModelId)}[/]");
            AnsiConsole.WriteLine();

            var topic = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold yellow]What would you like the personas to discuss?[/]")
                    .Validate(input => string.IsNullOrWhiteSpace(input)
                        ? ValidationResult.Error("[red]Please enter a non-empty topic.[/]")
                        : ValidationResult.Success()));

            IUserInterface ui = new ConsoleUserInterface();
            var builder = new ConversationPipelineBuilder(dataPaths, eventSink);
            var pipeline = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .StartAsync("Building conversation pipeline...", _ =>
                    builder.BuildAsync(
                        settings,
                        topic,
                        cancellationToken: default,
                        notify: message => AnsiConsole.MarkupLine($"[green]{Markup.Escape(message)}[/]")));

            AnsiConsole.MarkupLine($"[bold]Personas:[/] {string.Join(", ", pipeline.Personas.Select(p => Markup.Escape(p.Name)))}\n");
            await pipeline.CreateConversation(ui).StartConversationAsync(topic);
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
