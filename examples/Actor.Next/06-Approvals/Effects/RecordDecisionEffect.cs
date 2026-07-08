using System.Text.Json;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Examples.Approvals;

namespace Approvals.Next.Example06.Effects;

/// <summary>Records the approver's decision on the document.</summary>
internal sealed class RecordDecisionEffect : IActorEffect
{
    public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
    {
        var bag = ApprovalCapabilityRegistry.State(context);
        var decision = ApprovalCapabilityRegistry.Payload(context).Deserialize<Decision>();
        if (decision is not null)
        {
            bag.Set("approver", decision.Approver);
            bag.Set("decisionNote", decision.Note ?? string.Empty);
        }

        return ValueTask.CompletedTask;
    }
}
