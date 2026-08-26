namespace Dapr.Actors.Next.Examples.Approvals

open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions.Filters

type WithinApprovalLimitGuard() =
    interface IActorGuard with
        member _.EvaluateAsync(context: ActorCapabilityContext, _: CancellationToken) : ValueTask<bool> =
            let amount = CapabilityContext.State(context).Get<decimal>("amount")
            ValueTask.FromResult(amount <= ApprovalDefinitions.AutoApprovalLimit)
