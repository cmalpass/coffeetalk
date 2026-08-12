using CoffeeTalk.Core.Interfaces;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace CoffeeTalk.Gui.Services
{
    public class BlazorUserInterface : IUserInterface
    {
        // Event to notify Blazor components to re-render
        public event Action? OnChange;

        // Chat History
        private readonly object _messagesLock = new();
        public List<ChatMessage> Messages { get; } = new();

        // Current Status
        public string? StatusMessage { get; private set; }
        public bool IsBusy { get; private set; }

        // Document State
        public string DocumentContent { get; private set; } = "";

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
            lock (_messagesLock)
                Messages.Add(new ChatMessage { Sender = agentName, Content = response });
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ShowDocumentPreviewAsync(string content)
        {
            DocumentContent = content;
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
                NotifyStateChanged();

                _interventionTcs = new TaskCompletionSource<(string Action, string Message)>();
                return _interventionTcs.Task;
            }
        }

        // Called by UI component when user submits intervention
        public void SubmitIntervention(string action, string message)
        {
            lock (_tcsLock)
            {
                if (_interventionTcs == null) return;
                if (!_interventionTcs.TrySetResult((action, message))) return;
                _interventionTcs = null;
            }
            IsInterventionRequired = false;
            NotifyStateChanged();
        }

        public Task SetStatusAsync(string status)
        {
            StatusMessage = status;
            IsBusy = true;
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        public Task ClearStatusAsync()
        {
            StatusMessage = null;
            IsBusy = false;
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
             var sb = new StringBuilder();
             sb.AppendLine($"**Topic:** {topic}");
             sb.AppendLine($"**Participants:** {string.Join(", ", participants)}");
             sb.AppendLine($"**Mode:** {mode}");
             if (interactive) sb.AppendLine("*Interactive Mode Enabled*");

             lock (_messagesLock)
                 Messages.Add(new ChatMessage { Sender = "System", Content = sb.ToString(), IsSystem = true });
             NotifyStateChanged();
             return Task.CompletedTask;
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
