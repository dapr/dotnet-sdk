using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Examples.Approvals;

namespace Approvals.Next.Example06.Guards;

/// <summary>Passes when the document's amount is at or below the auto-approval limit.</summary>
internal sealed class WithinApprovalLimitGuard : IActorGuard
{
    public ValueTask<bool> EvaluateAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
    {
        var amount = ApprovalCapabilityRegistry.State(context).Get<decimal>("amount");
        return ValueTask.FromResult(amount <= ApprovalCapabilityRegistry.AutoApprovalLimit);
    }
}
