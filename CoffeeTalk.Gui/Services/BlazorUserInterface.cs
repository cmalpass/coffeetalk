using CoffeeTalk.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Markdig;

namespace CoffeeTalk.Gui.Services
{
    public class BlazorUserInterface : IUserInterface
    {
        // Event to notify Blazor components to re-render
        public event Action? OnChange;

        // Chat History
        public List<ChatMessage> Messages { get; } = new();
        public bool StopRequested { get; private set; }
        public string? ConversationTopic { get; private set; }
        public IReadOnlyList<string> ConversationParticipants { get; private set; } = Array.Empty<string>();
        public string? ConversationMode { get; private set; }
        public bool IsConversationRunning { get; private set; }
        public string? CurrentThinkingPersona { get; private set; }
        public DateTime? ConversationStartedAt { get; private set; }

        // Current Status
        public string? StatusMessage { get; private set; }
        public bool IsBusy { get; private set; }

        // Document State
        public string DocumentContent { get; private set; } = "";
        public string DocumentMarkdown { get; private set; } = "";
        public string DocumentHtml => Markdown.ToHtml(DocumentMarkdown, MarkdownPipeline);

        private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
            .DisableHtml()
            .UseAdvancedExtensions()
            .Build();

        // User Intervention
        private TaskCompletionSource<(string Action, string Message)>? _interventionTcs;
        public bool IsInterventionRequired { get; private set; }

        public void NotifyStateChanged() => OnChange?.Invoke();

        public Task ShowMessageAsync(string message)
        {
            Messages.Add(new ChatMessage { Sender = "System", Content = message, IsSystem = true });
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string message)
        {
            Messages.Add(new ChatMessage { Sender = "Error", Content = message, IsError = true });
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ShowAgentResponseAsync(string agentName, string response)
        {
            CurrentThinkingPersona = null;
            Messages.Add(new ChatMessage { Sender = agentName, Content = response });
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ShowDocumentPreviewAsync(string content)
        {
            DocumentContent = content;
            DocumentMarkdown = content;
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task<(string Action, string Message)> GetUserInterventionAsync()
        {
            IsInterventionRequired = true;
            NotifyStateChanged();

            _interventionTcs = new TaskCompletionSource<(string Action, string Message)>();
            return _interventionTcs.Task;
        }

        // Called by UI component when user submits intervention
        public void SubmitIntervention(string action, string message)
        {
            if (action == "quit")
            {
                StopRequested = true;
            }
            IsInterventionRequired = false;
            NotifyStateChanged();
            _interventionTcs?.SetResult((action, message));
        }

        public Task SetStatusAsync(string status)
        {
            StatusMessage = status;
            IsBusy = true;
            CurrentThinkingPersona = status.EndsWith(" is thinking...", StringComparison.Ordinal)
                ? status[..^" is thinking...".Length]
                : null;
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ClearStatusAsync()
        {
            StatusMessage = null;
            IsBusy = false;
            CurrentThinkingPersona = null;
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public async Task RunWithStatusAsync(string status, Func<Task> action)
        {
            await SetStatusAsync(status);
            try
            {
                await action();
            }
            finally
            {
                await ClearStatusAsync();
            }
        }

        public Task ShowConversationHeaderAsync(string topic, IReadOnlyCollection<string> participants, string mode, bool interactive)
        {
             ConversationTopic = topic;
             ConversationParticipants = participants.ToList();
             ConversationMode = interactive ? $"{mode} · Interactive" : mode;
             ConversationStartedAt = DateTime.Now;
             IsConversationRunning = true;
             StopRequested = false;
             NotifyStateChanged();
             return Task.CompletedTask;
        }

        public void EndConversation()
        {
            IsConversationRunning = false;
            CurrentThinkingPersona = null;
            IsInterventionRequired = false;
            NotifyStateChanged();
        }

        public void LoadConversation(ConversationRecord record)
        {
            Messages.Clear();
            Messages.AddRange(record.Messages.Select(message => new ChatMessage
            {
                Sender = message.Sender,
                Content = message.Content,
                IsSystem = message.IsSystem,
                IsError = message.IsError,
                IsDivider = message.IsDivider,
                Timestamp = message.Timestamp
            }));
            ConversationTopic = record.Topic;
            ConversationParticipants = record.Personas;
            ConversationMode = "History";
            ConversationStartedAt = record.StartedAt;
            IsConversationRunning = false;
            NotifyStateChanged();
        }

        public Task ShowRuleAsync(string title = "")
        {
            // Can be represented as a divider in UI
            Messages.Add(new ChatMessage { IsDivider = true, Content = title });
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ShowMarkupLineAsync(string message)
        {
            return ShowMessageAsync(message);
        }
    }

    public class ChatMessage
    {
        public string Sender { get; set; } = "";
        public string Content { get; set; } = "";
        public bool IsSystem { get; set; }
        public bool IsError { get; set; }
        public bool IsDivider { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
