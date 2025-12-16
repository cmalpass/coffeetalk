using CoffeeTalk.Core.Interfaces;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoffeeTalk.Services
{
    public class ConsoleUserInterface : IUserInterface
    {
        public Task ShowMessageAsync(string message)
        {
            AnsiConsole.MarkupLine(message);
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string message)
        {
            AnsiConsole.MarkupLine($"[red]{message}[/]");
            return Task.CompletedTask;
        }

        public Task ShowAgentResponseAsync(string agentName, string response)
        {
            var panel = new Panel(new Text(response))
                .Header($"[bold]{Markup.Escape(agentName)}[/]")
                .Border(BoxBorder.Rounded);

            AnsiConsole.Write(panel);
            return Task.CompletedTask;
        }

        public Task ShowDocumentPreviewAsync(string content)
        {
            var panel = new Panel(new Text(content))
                .Header("[bold cyan]Document State[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan1);

            AnsiConsole.Write(panel);
            return Task.CompletedTask;
        }

        public Task<(string Action, string Message)> GetUserInterventionAsync()
        {
            AnsiConsole.WriteLine();
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Director's Chair[/]: What would you like to do?")
                    .AddChoices(new[] {
                        "Continue",
                        "Inject Direction/Feedback",
                        "End Conversation"
                    }));

            if (selection == "End Conversation")
            {
                return Task.FromResult(("quit", string.Empty));
            }

            if (selection == "Inject Direction/Feedback")
            {
                var message = AnsiConsole.Ask<string>("[green]Enter your instruction:[/]");
                return Task.FromResult(("inject", message));
            }

            return Task.FromResult(("continue", string.Empty));
        }

        public Task SetStatusAsync(string status)
        {
            // In console, status is usually managed by the Spinner callback.
            // But if we are inside a spinner callback, we can't easily change the status text of the parent
            // without access to the Context.
            // However, AnsiConsole.Status() is a blocking call that wraps a task.
            // The RunWithStatusAsync method handles the wrapping.
            // This method might be no-op or log if not inside a context.
            // For now, we will just print it if we can't update a spinner.
            // But ideally we rely on RunWithStatusAsync.
            AnsiConsole.MarkupLine($"[dim]{status}[/]");
            return Task.CompletedTask;
        }

        public Task ClearStatusAsync()
        {
            // No-op for console as status clears when task ends
            return Task.CompletedTask;
        }

        public async Task RunWithStatusAsync(string status, Func<Task> action)
        {
             await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(status, async ctx =>
                {
                    // If we wanted to support SetStatusAsync updating this spinner,
                    // we would need to store ctx in a thread-local or similar,
                    // but for simplicity, we just run the action.
                    await action();
                });
        }

        public Task ShowConversationHeaderAsync(string topic, IReadOnlyCollection<string> participants, string mode, bool interactive)
        {
            AnsiConsole.MarkupLine($"\n[bold]🎯 Topic:[/] [cyan]{Markup.Escape(topic)}[/]\n");
            AnsiConsole.MarkupLine($"[bold]Participants:[/] {string.Join(", ", participants.Select(p => Markup.Escape(p)))}\n");
            AnsiConsole.MarkupLine($"[bold]Mode:[/] {mode}\n");

            if (interactive)
            {
                AnsiConsole.MarkupLine("[bold]Interactive Mode:[/] [green]Enabled (Director's Chair)[/]");
                AnsiConsole.MarkupLine("[dim]You will be prompted to intervene after each turn.[/]\n");
            }
            return Task.CompletedTask;
        }

        public Task ShowRuleAsync(string title = "")
        {
            AnsiConsole.Write(new Rule(title));
            return Task.CompletedTask;
        }

        public Task ShowMarkupLineAsync(string message)
        {
            AnsiConsole.MarkupLine(message);
            return Task.CompletedTask;
        }
    }
}
