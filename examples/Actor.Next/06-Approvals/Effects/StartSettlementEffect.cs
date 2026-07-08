using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Examples.Approvals;
using Dapr.Workflow;

namespace Approvals.Next.Example06.Effects;

/// <summary>
/// The composition seam: on entry to <c>Approved</c>, schedules the settlement workflow. The instance
/// id is derived deterministically from the actor type and id, so an at-least-once re-run of the
/// approving turn re-schedules the same instance rather than starting a second workflow.
/// </summary>
public sealed partial class StartSettlementEffect(IDaprWorkflowClient workflowClient, ILogger logger) : IActorEffect
{
    /// <summary>The name the settlement workflow is registered under.</summary>
    private const string WorkflowName = "SettlementWorkflow";

    /// <summary>Builds the deterministic workflow instance id for a document.</summary>
    public static string InstanceIdFor(string actorType, string documentId) => $"settlement-{actorType}-{documentId}";

    public async ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
    {
        var bag = ApprovalCapabilityRegistry.State(context);
        var documentId = context.ActorId.Value;
        var input = new SettlementInput(
            DocumentId: documentId,
            DocumentType: bag.Get<string>("documentType") ?? context.ActorType,
            Requester: bag.Get<string>("requester") ?? string.Empty,
            Amount: bag.Get<decimal>("amount"),
            Parties: bag.Get<string[]>("parties") ?? [],
            SimulateChargeFailure: bag.Get<bool>("simulateChargeFailure"));

        var instanceId = InstanceIdFor(context.ActorType, documentId);
        LogExecution(documentId, instanceId);

        await workflowClient.ScheduleNewWorkflowAsync(WorkflowName, instanceId, input);
    }

    [LoggerMessage(LogLevel.Information, "Document {DocumentId} approved; starting settlement workflow {InstanceId}.")]
    private partial void LogExecution(string documentId, string instanceId);
}
