using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Models;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoffeeTalk.Services
{
    public class ConsoleUserInterface : IUserInterface
    {
        private readonly object _sync = new();
        private bool _stopRequested;

        /// <summary>
        /// True when the user has signaled that the conversation should stop.
        ///
        /// Polled between turns by the conversation loop. The console host (Program.cs)
        /// sets <see cref="RequestStop()"/> from a Ctrl+C handler, and this getter also
        /// opportunistically drains any pending 'q'/Esc keypress from stdin so a keystroke
        /// typed between turns is honored. When stdin is redirected (non-interactive) the
        /// keypress path is skipped entirely and only <see cref="RequestStop()"/> can stop.
        /// </summary>
        public bool StopRequested
        {
            get
            {
                if (!Console.IsInputRedirected)
                {
                    TryDrainStopKey();
                }

                lock (_sync)
                {
                    return _stopRequested;
                }
            }
        }

        public ConversationTerminationReason TerminationReason { get; set; }
        public string? ConversationTopic { get; private set; }
        public IReadOnlyList<string> ConversationParticipants { get; private set; } = Array.Empty<string>();
        public DateTimeOffset? ConversationStartedAt { get; private set; }
        public string DocumentContent { get; private set; } = string.Empty;
        public List<ConsoleMessage> Messages { get; } = new();
        private int? _streamingMessageIndex;

        public Task ShowMessageAsync(string message)
        {
            Messages.Add(new ConsoleMessage("System", message, true, false, false));
            AnsiConsole.MarkupLine(message);
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string message)
        {
            Messages.Add(new ConsoleMessage("Error", message, false, true, false));
            AnsiConsole.MarkupLine($"[red]{message}[/]");
            return Task.CompletedTask;
        }

        public Task ShowAgentResponseAsync(string agentName, string response)
        {
            if (_streamingMessageIndex is int index)
            {
                Messages[index] = Messages[index] with { Content = response };
                AnsiConsole.WriteLine();
                _streamingMessageIndex = null;
                return Task.CompletedTask;
            }

            Messages.Add(new ConsoleMessage(agentName, response, false, false, false));
            var panel = new Panel(new Text(response))
                .Header($"[bold]{Markup.Escape(agentName)}[/]")
                .Border(BoxBorder.Rounded);

            AnsiConsole.Write(panel);
            return Task.CompletedTask;
        }

        public Task ShowAgentResponseChunkAsync(string agentName, string chunk)
        {
            if (_streamingMessageIndex is not int index)
            {
                Messages.Add(new ConsoleMessage(agentName, string.Empty, false, false, false));
                AnsiConsole.Markup($"[bold]{Markup.Escape(agentName)}[/] ");
                index = Messages.Count - 1;
                _streamingMessageIndex = index;
            }

            var message = Messages[index];
            Messages[index] = message with { Content = message.Content + chunk };
            AnsiConsole.Write(new Text(chunk));
            return Task.CompletedTask;
        }

        public Task ShowDocumentPreviewAsync(string content)
        {
            DocumentContent = content;
            var panel = new Panel(new Text(content))
                .Header("[bold cyan]Document State[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan1);

            AnsiConsole.Write(panel);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Signal that the conversation should stop. Thread-safe; typically called from a
        /// Ctrl+C (<see cref="Console.CancelKeyPress"/>) handler in the console host.
        /// </summary>
        public void RequestStop()
        {
            lock (_sync)
            {
                _stopRequested = true;
            }
        }

        private void TryDrainStopKey()
        {
            while (Console.KeyAvailable)
            {
                if (ConsoleStopDecision.ShouldStop(Console.ReadKey(true)))
                {
                    RequestStop();
                }
            }
        }

        public Task<(string Action, string Message)> GetUserInterventionAsync()
        {
            AnsiConsole.WriteLine();
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Director's Chair[/]: What would you like to do?")
                    .AddChoices("Continue", "Inject Direction/Feedback", "End Conversation"));

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
            ConversationTopic = topic;
            ConversationParticipants = participants.ToList();
            ConversationStartedAt = DateTimeOffset.Now;
            AnsiConsole.MarkupLine($"\n[bold]🎯 Topic:[/] [cyan]{Markup.Escape(topic)}[/]\n");
            AnsiConsole.MarkupLine($"[bold]Participants:[/] {string.Join(", ", participants.Select(p => Markup.Escape(p)))}\n");
            AnsiConsole.MarkupLine($"[bold]Mode:[/] {mode}\n");

            if (!Console.IsInputRedirected)
            {
                AnsiConsole.MarkupLine("[dim]Press 'q' or Ctrl+C between turns to stop the conversation.[/]");
            }

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

    public sealed record ConsoleMessage(string Sender, string Content, bool IsSystem, bool IsError, bool IsDivider);
}
