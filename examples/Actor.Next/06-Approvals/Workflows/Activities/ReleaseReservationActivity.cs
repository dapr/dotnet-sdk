using Dapr.Workflow;

namespace Dapr.Actors.Next.Examples.Approvals.Workflows.Activities;

/// <summary>
/// The compensation step: undoes any partial provisioning made before the charge failed. Runs only on
/// the failure path and must be idempotent.
/// </summary>
public sealed partial class ReleaseReservationActivity(ILogger<ReleaseReservationActivity> logger) : WorkflowActivity<ReleaseRequest, object?>
{
    public override Task<object?> RunAsync(WorkflowActivityContext context, ReleaseRequest input)
    {
        LogReleasingInformation(input.Amount, input.DocumentId);
        return Task.FromResult<object?>(null);
    }

    [LoggerMessage(LogLevel.Information, "Released reservation of {Amount:C} for document {DocumentId}")]
    private partial void LogReleasingInformation(decimal amount, string documentId);
}
