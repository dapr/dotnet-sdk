namespace Approvals.Next.FSharp.Example06.Tests

open System
open System.Collections.Generic
open System.Text.Json
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Interpreted
open Dapr.Actors.Next.Examples.Approvals
open Dapr.Workflow
open Xunit

type SettlementCompositionTests() =

    [<Fact>]
    member this.Approving_schedules_settlement_with_a_deterministic_id_and_does_not_double_schedule() = task {
        let workflowClient = new RecordingWorkflowClient()
        let effect = StartSettlementEffect(workflowClient :> IDaprWorkflowClient, NullLogger.Instance :> ILogger)
        let context = this.ApprovedContext("exp-9", 250m, [| "finance"; "alice" |])

        let effectInterface = effect :> IActorEffect
        let t1 = effectInterface.ExecuteAsync(context)
        do! t1.AsTask()
        let t2 = effectInterface.ExecuteAsync(context)
        do! t2.AsTask()

        let expected = StartSettlementEffect.InstanceIdFor(ApprovalDefinitions.ActorType, "exp-9")
        Assert.True(workflowClient.Scheduled.Count = 2)
        for id in workflowClient.Scheduled do
            Assert.Equal(expected, id)
        let distinctCount = workflowClient.Scheduled |> Seq.distinct |> Seq.length
        Assert.Equal(1, distinctCount)
    }

    member private _.ApprovedContext(documentId: string, amount: decimal, parties: string[]) : ActorCapabilityContext =
        let bag = DynamicStateBag()
        bag.Set("documentType", "ExpenseReport")
        bag.Set("requester", "alice")
        bag.Set("amount", amount)
        bag.Set("parties", parties)
        bag.Set("simulateChargeFailure", false)

        let args = Dictionary<string, obj>(StringComparer.Ordinal)
        args.["state"] <- box bag
        args.["event"] <- box "Approve"
        args.["payload"] <- box (JsonSerializer.SerializeToElement(obj()))
        args.["documentVersion"] <- box 1

        let baggage = Dictionary<string, string>(StringComparer.Ordinal) :> IReadOnlyDictionary<string, string>

        ActorCapabilityContext(
            ApprovalDefinitions.ActorType,
            ActorId.Create(documentId),
            ApprovalDefinitions.StartSettlementEffect,
            args :> IReadOnlyDictionary<string, obj>,
            ActorRequestContext(null, null, baggage))
