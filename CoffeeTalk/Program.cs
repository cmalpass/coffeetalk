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
            var persistence = new ConversationPersistenceService(dataPaths);
            if (await HandleHistoryCommandAsync(args, persistence))
                return;

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
            if (args.Contains("--save", StringComparer.OrdinalIgnoreCase))
            {
                var consoleUi = (ConsoleUserInterface)ui;
                var saveId = GetOption(args, "--save") ?? Guid.NewGuid().ToString("N");
                await persistence.SaveAsync(new ConversationState
                {
                    Id = saveId,
                    Topic = consoleUi.ConversationTopic ?? topic,
                    Participants = consoleUi.ConversationParticipants.Select(name => new ConversationParticipant { Name = name }).ToList(),
                    StartedAt = consoleUi.ConversationStartedAt ?? DateTimeOffset.Now,
                    CompletedAt = DateTimeOffset.Now,
                    DocumentContent = consoleUi.DocumentContent,
                    Messages = consoleUi.Messages.Select(message => new ConversationMessage
                    {
                        Sender = message.Sender, Content = message.Content, IsSystem = message.IsSystem,
                        IsError = message.IsError, IsDivider = message.IsDivider, Timestamp = DateTimeOffset.Now
                    }).ToList()
                });
                AnsiConsole.MarkupLine($"[green]Saved conversation {Markup.Escape(saveId)}[/]");
            }
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

    private static async Task<bool> HandleHistoryCommandAsync(string[] args, ConversationPersistenceService persistence)
    {
        if (args.Contains("--list", StringComparer.OrdinalIgnoreCase))
        {
            foreach (var state in await persistence.ListAsync())
                AnsiConsole.MarkupLine($"{Markup.Escape(state.Id)}  {Markup.Escape(state.Topic)}  {state.Status}");
            return true;
        }

        var deleteId = GetOption(args, "--delete");
        if (deleteId is not null)
        {
            await persistence.DeleteAsync(deleteId);
            AnsiConsole.MarkupLine($"[green]Deleted conversation {Markup.Escape(deleteId)}[/]");
            return true;
        }

        var resumeId = GetOption(args, "--resume");
        if (resumeId is not null)
        {
            var state = await persistence.ResumeAsync(resumeId);
            AnsiConsole.MarkupLine($"[bold]Topic:[/] {Markup.Escape(state.Topic)}");
            AnsiConsole.MarkupLine($"[bold]Status:[/] {Markup.Escape(state.Status)}");
            AnsiConsole.WriteLine(state.DocumentContent);
            foreach (var message in state.Messages)
                AnsiConsole.MarkupLine($"[bold]{Markup.Escape(message.Sender)}:[/] {Markup.Escape(message.Content)}");
            return true;
        }

        return false;
    }

    private static string? GetOption(string[] args, string option)
    {
        var index = Array.FindIndex(args, value => value.Equals(option, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal)
            ? args[index + 1]
            : null;
    }
}
