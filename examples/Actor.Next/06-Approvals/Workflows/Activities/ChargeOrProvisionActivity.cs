using Dapr.Workflow;

namespace Dapr.Actors.Next.Examples.Approvals.Workflows.Activities;

/// <summary>
/// Charges (or provisions) for an approved document. This is the failure-prone external step: when the
/// request is marked to simulate a failure it throws, so the workflow's retry policy re-invokes it and,
/// once retries are exhausted, the workflow compensates. In a real deployment this would call a payment
/// or provisioning service and must be idempotent.
/// </summary>
public sealed partial class ChargeOrProvisionActivity(ILogger<ChargeOrProvisionActivity> logger) : WorkflowActivity<ChargeRequest, object?>
{
    public override Task<object?> RunAsync(WorkflowActivityContext context, ChargeRequest input)
    {
        if (input.SimulateFailure)
        {
            LogChargeFailed(input.DocumentId);
            throw new InvalidOperationException($"Charge for document '{input.DocumentId}' was declined");
        }

        LogChargeSucceeded(input.Amount, input.DocumentId);
        return Task.FromResult<object?>(null);
    }

    [LoggerMessage(LogLevel.Warning, "Charge for {DocumentId} failed (simulated)")]
    private partial void LogChargeFailed(string documentId);

    [LoggerMessage(LogLevel.Information, "Charged {Amount:C} for document {DocumentId}")]
    private partial void LogChargeSucceeded(decimal amount, string documentId);
}
