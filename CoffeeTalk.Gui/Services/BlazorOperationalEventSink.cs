using CoffeeTalk.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoffeeTalk.Gui.Services;

public sealed class BlazorOperationalEventSink(ILogger<BlazorOperationalEventSink> logger) : IOperationalEventSink
{
    public void Publish(OperationalEvent operationalEvent)
    {
        logger.LogInformation(
            "Operational event {EventKind} for {Operation}; attempt {Attempt}/{MaxRetries}; decision {Decision}; reason {Reason}",
            operationalEvent.Kind,
            operationalEvent.Operation,
            operationalEvent.Attempt,
            operationalEvent.MaxRetries,
            operationalEvent.Decision,
            operationalEvent.Reason);
    }
}
