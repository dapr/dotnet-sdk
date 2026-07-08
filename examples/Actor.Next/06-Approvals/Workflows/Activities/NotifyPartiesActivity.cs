using Dapr.Workflow;

namespace Dapr.Actors.Next.Examples.Approvals.Workflows.Activities;

/// <summary>
/// Notifies one party that a document has been approved. Called once per party as the workflow's
/// fan-out step. Idempotent: re-running it just logs again.
/// </summary>
public sealed partial class NotifyPartiesActivity(ILogger<NotifyPartiesActivity> logger) : WorkflowActivity<PartyNotification, object?>
{
    public override Task<object?> RunAsync(WorkflowActivityContext context, PartyNotification input)
    {
        LogNotification(input.Party, input.DocumentId);
        return Task.FromResult<object?>(null);
    }

    [LoggerMessage(LogLevel.Information, "Notifying {Party} about approved document {DocumentId}")]
    private partial void LogNotification(string party, string documentId);
}
