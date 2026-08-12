using Microsoft.Extensions.Configuration;
using CoffeeTalk.Models;
using CoffeeTalk.Services;
using CoffeeTalk.Core.Interfaces;
using CoffeeTalk.Helpers;
using Spectre.Console;
using System.Text.Json;

namespace CoffeeTalk;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var dataPaths = new ApplicationDataPathResolver();
            var workspaces = new WorkspaceService(dataPaths);
            if (await HandleWorkspaceCommandAsync(args, workspaces))
                return;

            var persistence = new ConversationPersistenceService(dataPaths);
            await MigrateLegacyHistoryAsync(dataPaths, persistence);
            if (await HandleAnalyticsCommandAsync(args, persistence))
                return;
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

            var exportFormat = GetOption(args, "--export-format");
            if (exportFormat is not null)
            {
                await ExportDocumentAsync(
                    dataPaths,
                    new PdfDocumentExporter(),
                    exportFormat,
                    ((ConsoleUserInterface)ui).DocumentContent,
                    topic);
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

    private static async Task MigrateLegacyHistoryAsync(
        IApplicationDataPathResolver paths,
        ConversationPersistenceService persistence)
    {
        var legacyPath = paths.ResolveDataPath("conversation-history.json", "conversation-history.json");
        if (!File.Exists(legacyPath))
            return;

        try
        {
            var records = JsonSerializer.Deserialize<List<LegacyConversationRecord>>(
                await File.ReadAllTextAsync(legacyPath),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            foreach (var record in records)
            {
                await persistence.SaveAsync(new ConversationState
                {
                    Id = record.Id,
                    Topic = record.Topic,
                    StartedAt = record.StartedAt,
                    CompletedAt = record.CompletedAt,
                    Status = record.Status,
                    DocumentContent = record.DocumentContent,
                    Participants = record.Personas.Select(name => new ConversationParticipant { Name = name }).ToList(),
                    Messages = record.Messages,
                    Metadata = record.Metadata
                });
            }
            File.Delete(legacyPath);
        }
        catch (JsonException)
        {
            AnsiConsole.MarkupLine("[yellow]Legacy conversation history is invalid; preserving it for recovery.[/]");
        }
    }

    private sealed class LegacyConversationRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Topic { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string Status { get; set; } = "Completed";
        public List<string> Personas { get; set; } = [];
        public List<ConversationMessage> Messages { get; set; } = [];
        public string DocumentContent { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = [];
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

    private static async Task<bool> HandleAnalyticsCommandAsync(
        string[] args, ConversationPersistenceService persistence)
    {
        if (!string.Equals(args.FirstOrDefault(), "stats", StringComparison.OrdinalIgnoreCase))
            return false;

        var sessionId = GetOption(args, "--session");
        if (sessionId is not null)
        {
            var state = await persistence.ResumeAsync(sessionId);
            PrintMetrics(state.Topic, state.Metrics);
            return true;
        }

        var summary = ConversationMetricsAggregator.Summarize(await persistence.ListAsync());
        AnsiConsole.MarkupLine($"[bold]Conversations:[/] {summary.ConversationCount}");
        AnsiConsole.MarkupLine($"[bold]Messages:[/] {summary.MessageCount}");
        AnsiConsole.MarkupLine($"[bold]Words:[/] {summary.WordCount}");
        AnsiConsole.MarkupLine($"[bold]Estimated tokens:[/] {summary.EstimatedTokenCount} (local approximation)");
        AnsiConsole.MarkupLine($"[bold]Average duration:[/] {FormatDuration(summary.AverageDuration)}");
        if (summary.MessagesByParticipant.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Messages by participant:[/]");
            foreach (var participant in summary.MessagesByParticipant.OrderByDescending(pair => pair.Value))
                AnsiConsole.MarkupLine($"  {Markup.Escape(participant.Key)}: {participant.Value}");
        }
        return true;
    }

    private static void PrintMetrics(string topic, ConversationMetrics metrics)
    {
        AnsiConsole.MarkupLine($"[bold]Topic:[/] {Markup.Escape(topic)}");
        AnsiConsole.MarkupLine($"[bold]Messages:[/] {metrics.MessageCount}");
        AnsiConsole.MarkupLine($"[bold]Words:[/] {metrics.WordCount}");
        AnsiConsole.MarkupLine($"[bold]Estimated tokens:[/] {metrics.EstimatedTokenCount} (local approximation)");
        AnsiConsole.MarkupLine($"[bold]Duration:[/] {FormatDuration(metrics.Duration)}");
        AnsiConsole.MarkupLine($"[bold]Document:[/] {metrics.DocumentWordCount} words, {metrics.DocumentHeadingCount} headings");
        foreach (var participant in metrics.MessagesByParticipant.OrderByDescending(pair => pair.Value))
            AnsiConsole.MarkupLine($"  {Markup.Escape(participant.Key)}: {participant.Value} messages, {metrics.WordsByParticipant.GetValueOrDefault(participant.Key)} words");
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : $"{(int)duration.TotalMinutes}m {duration.Seconds}s";

    private static async Task<bool> HandleWorkspaceCommandAsync(string[] args, WorkspaceService workspaces)
    {
        var command = args.FirstOrDefault();
        if (command is null || command.StartsWith("-", StringComparison.Ordinal))
            return false;

        switch (command.ToLowerInvariant())
        {
            case "list":
                foreach (var workspace in await workspaces.ListAsync())
                    AnsiConsole.MarkupLine($"{(workspace.Id == workspaces.Active.Id ? "[green]*[/]" : " ")} {Markup.Escape(workspace.Name)} ({Markup.Escape(workspace.Id)})");
                return true;
            case "new":
                var name = GetOption(args, "--name")
                    ?? throw new ArgumentException("The new command requires --name.");
                var created = await workspaces.CreateAsync(name);
                await workspaces.SwitchAsync(created.Id);
                AnsiConsole.MarkupLine($"[green]Created and switched to workspace {Markup.Escape(created.Name)}[/]");
                return true;
            case "switch":
                var switchName = args.Length > 1 && !args[1].StartsWith("-", StringComparison.Ordinal) ? args[1] : null;
                if (string.IsNullOrWhiteSpace(switchName))
                    throw new ArgumentException("The switch command requires a workspace name or id.");
                var selected = await workspaces.SwitchAsync(switchName);
                AnsiConsole.MarkupLine($"[green]Switched to workspace {Markup.Escape(selected.Name)}[/]");
                return true;
            case "delete":
                var deleteName = args.Length > 1 && !args[1].StartsWith("-", StringComparison.Ordinal) ? args[1] : null;
                if (string.IsNullOrWhiteSpace(deleteName))
                    throw new ArgumentException("The delete command requires a workspace name or id.");
                if (!AnsiConsole.Confirm($"Delete workspace '{Markup.Escape(deleteName)}' and all its conversations?", false))
                    return true;
                await workspaces.DeleteAsync(deleteName);
                AnsiConsole.MarkupLine($"[green]Deleted workspace {Markup.Escape(deleteName)}[/]");
                return true;
            default:
                return false;
        }
    }

    private static string? GetOption(string[] args, string option)
    {
        var index = Array.FindIndex(args, value => value.Equals(option, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal)
            ? args[index + 1]
            : null;
    }

    private static async Task ExportDocumentAsync(
        IApplicationDataPathResolver dataPaths,
        IPdfDocumentExporter pdfExporter,
        string format,
        string content,
        string topic)
    {
        var normalizedFormat = format.ToLowerInvariant();
        if (normalizedFormat is not ("markdown" or "md" or "pdf"))
            throw new ArgumentException("Export format must be markdown or pdf.", nameof(format));

        var extension = normalizedFormat == "pdf" ? "pdf" : "md";
        var safeTopic = new string(topic.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) || character is '/' or '\\'
                ? '_'
                : character).ToArray()).Trim();
        safeTopic = string.IsNullOrWhiteSpace(safeTopic) ? "conversation" : safeTopic;
        var path = dataPaths.ResolveExportPath(
            $"{safeTopic}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}",
            $"conversation.{extension}");

        if (normalizedFormat == "pdf")
            await pdfExporter.ExportAsync(content, path);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content);
        }

        AnsiConsole.MarkupLine($"[green]Exported to {Markup.Escape(path)}[/]");
    }
}
