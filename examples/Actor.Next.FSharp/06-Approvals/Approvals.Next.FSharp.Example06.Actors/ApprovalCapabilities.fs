namespace Dapr.Actors.Next.Examples.Approvals

open System
open System.Collections.Generic
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Workflow

type ApprovalCapabilityRegistry(workflowClient: IDaprWorkflowClient, ?loggerFactory: ILoggerFactory) =
    let loggerFactory = defaultArg loggerFactory (NullLoggerFactory.Instance :> ILoggerFactory)
    let logger = loggerFactory.CreateLogger<ApprovalCapabilityRegistry>()

    let effects = Dictionary<string, IActorEffect>(StringComparer.Ordinal)
    let guards = Dictionary<string, IActorGuard>(StringComparer.Ordinal)

    do
        effects.[ApprovalDefinitions.RecordSubmissionEffect] <- RecordSubmissionEffect() :> IActorEffect
        effects.[ApprovalDefinitions.RecordDecisionEffect] <- RecordDecisionEffect() :> IActorEffect
        effects.[ApprovalDefinitions.NotifyManagerEffect] <- LogEffect(logger, "escalated to a manager") :> IActorEffect
        effects.[ApprovalDefinitions.RecordSettlementFailureEffect] <- RecordSettlementFailureEffect() :> IActorEffect
        effects.[ApprovalDefinitions.StartSettlementEffect] <- StartSettlementEffect(workflowClient, logger) :> IActorEffect

        guards.[ApprovalDefinitions.WithinApprovalLimitGuard] <- WithinApprovalLimitGuard() :> IActorGuard

    interface ICapabilityRegistry with
        member _.TryGetEffect(name: string, effect: byref<IActorEffect>) : bool =
            match effects.TryGetValue(name) with
            | true, e -> effect <- e; true
            | false, _ -> effect <- Unchecked.defaultof<_>; false

        member _.TryGetGuard(name: string, guard: byref<IActorGuard>) : bool =
            match guards.TryGetValue(name) with
            | true, g -> guard <- g; true
            | false, _ -> guard <- Unchecked.defaultof<_>; false
