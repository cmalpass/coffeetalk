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
        private readonly object _messagesLock = new();
        public List<ChatMessage> Messages { get; } = new();
        public IReadOnlyList<ChatMessage> GetMessagesSnapshot()
        {
            lock (_messagesLock)
                return Messages.ToList();
        }
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
        private readonly object _tcsLock = new();
        private TaskCompletionSource<(string Action, string Message)>? _interventionTcs;
        public bool IsInterventionRequired { get; private set; }

        public void NotifyStateChanged() => OnChange?.Invoke();

        public Task ShowMessageAsync(string message)
        {
            lock (_messagesLock)
                Messages.Add(new ChatMessage { Sender = "System", Content = message, IsSystem = true });
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string message)
        {
            lock (_messagesLock)
                Messages.Add(new ChatMessage { Sender = "Error", Content = message, IsError = true });
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ShowAgentResponseAsync(string agentName, string response)
        {
            CurrentThinkingPersona = null;
            lock (_messagesLock)
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
            lock (_tcsLock)
            {
                if (_interventionTcs != null)
                    return _interventionTcs.Task; // Already waiting for intervention

                IsInterventionRequired = true;
                _interventionTcs = new TaskCompletionSource<(string Action, string Message)>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                NotifyStateChanged();
                return _interventionTcs.Task;
            }
        }

        // Called by UI component when user submits intervention
        public void SubmitIntervention(string action, string message)
        {
            if (action == "quit")
            {
                StopRequested = true;
            }
            lock (_tcsLock)
            {
                if (_interventionTcs == null) return;
                if (!_interventionTcs.TrySetResult((action, message))) return;
                _interventionTcs = null;
            }
            IsInterventionRequired = false;
            NotifyStateChanged();
        }

        public void CancelIntervention(bool markStop = true)
        {
            TaskCompletionSource<(string Action, string Message)>? tcs;
            lock (_tcsLock)
            {
                tcs = _interventionTcs;
                _interventionTcs = null;
                IsInterventionRequired = false;
                if (markStop)
                    StopRequested = true;
            }

            tcs?.TrySetCanceled();
            if (tcs is not null)
                NotifyStateChanged();
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
            CancelIntervention(false);
            NotifyStateChanged();
        }

        public void ResetForNewConversation()
        {
            CancelIntervention();
            lock (_messagesLock)
                Messages.Clear();
            StopRequested = false;
            ConversationTopic = null;
            ConversationParticipants = Array.Empty<string>();
            ConversationMode = null;
            ConversationStartedAt = null;
            IsConversationRunning = false;
            StatusMessage = null;
            IsBusy = false;
            CurrentThinkingPersona = null;
            DocumentContent = "";
            DocumentMarkdown = "";
            NotifyStateChanged();
        }

        public void LoadConversation(ConversationRecord record)
        {
            lock (_messagesLock)
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
            }
            ConversationTopic = record.Topic;
            ConversationParticipants = record.Personas;
            ConversationMode = "History";
            ConversationStartedAt = record.StartedAt;
            IsConversationRunning = false;
            StopRequested = false;
            StatusMessage = null;
            IsBusy = false;
            CurrentThinkingPersona = null;
            DocumentContent = record.DocumentContent;
            DocumentMarkdown = record.DocumentContent;
            NotifyStateChanged();
        }

        public Task ShowRuleAsync(string title = "")
        {
            lock (_messagesLock)
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
