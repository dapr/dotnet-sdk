using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Examples.Approvals;

namespace Approvals.Next.Example06.Effects;

/// <summary>Flags that the document's settlement failed after compensation ran.</summary>
internal sealed class RecordSettlementFailureEffect : IActorEffect
{
    public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
    {
        ApprovalCapabilityRegistry.State(context).Set("settlementFailed", true);
        return ValueTask.CompletedTask;
    }
}