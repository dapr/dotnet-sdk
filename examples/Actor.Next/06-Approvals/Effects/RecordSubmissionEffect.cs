using System.Text.Json;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Examples.Approvals;

namespace Approvals.Next.Example06.Effects;

/// <summary>Copies the submitted document details from the event payload into the actor's state.</summary>
internal sealed class RecordSubmissionEffect : IActorEffect
{
    public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
    {
        var bag = ApprovalCapabilityRegistry.State(context);
        var submission = ApprovalCapabilityRegistry.Payload(context).Deserialize<SubmitDocument>()
                         ?? throw new InvalidOperationException("Submit payload is required.");

        bag.Set("requester", submission.Requester);
        bag.Set("amount", submission.Amount);
        bag.Set("parties", submission.Parties);
        bag.Set("simulateChargeFailure", submission.SimulateChargeFailure);
        return ValueTask.CompletedTask;
    }
}