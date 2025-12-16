
using System;
using System.Threading.Tasks;

namespace CoffeeTalk.Core.Interfaces
{
    public interface IUserInterface
    {
        Task ShowMessageAsync(string message);
        Task ShowErrorAsync(string message);
        Task ShowAgentResponseAsync(string agentName, string response);
        Task ShowDocumentPreviewAsync(string content);
        Task<(string Action, string Message)> GetUserInterventionAsync();

        // Status methods
        Task SetStatusAsync(string status);
        Task ClearStatusAsync();
        Task RunWithStatusAsync(string status, Func<Task> action);

        // Additional UI methods
        Task ShowConversationHeaderAsync(string topic, IReadOnlyCollection<string> participants, string mode, bool interactive);
        Task ShowRuleAsync(string title = "");
        Task ShowMarkupLineAsync(string message);

        // Helper to mimic AnsiConsole.Status().Spinner().StartAsync()
        // But simplified for generic UI.
    }
}
