namespace Dapr.Actors.Next.Examples.Approvals

open System
open System.Collections.Generic
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Workflow

type RecordSubmissionEffect() =
    interface IActorEffect with
        member _.ExecuteAsync(context: ActorCapabilityContext, _: CancellationToken) : ValueTask =
            let bag = CapabilityContext.State(context)
            let submission = CapabilityContext.Payload(context).Deserialize<SubmitDocument>()
            if isNull (box submission) then
                raise (InvalidOperationException("Submit payload is required."))
            bag.Set("requester", submission.Requester)
            bag.Set("amount", submission.Amount)
            bag.Set("parties", submission.Parties)
            bag.Set("simulateChargeFailure", submission.SimulateChargeFailure)
            ValueTask.CompletedTask

type RecordDecisionEffect() =
    interface IActorEffect with
        member _.ExecuteAsync(context: ActorCapabilityContext, _: CancellationToken) : ValueTask =
            let bag = CapabilityContext.State(context)
            let decision = CapabilityContext.Payload(context).Deserialize<Decision>()
            if not (isNull (box decision)) then
                bag.Set("approver", decision.Approver)
                bag.Set("decisionNote", if isNull decision.Note then String.Empty else decision.Note)
            ValueTask.CompletedTask

type RecordSettlementFailureEffect() =
    interface IActorEffect with
        member _.ExecuteAsync(context: ActorCapabilityContext, _: CancellationToken) : ValueTask =
            CapabilityContext.State(context).Set("settlementFailed", true)
            ValueTask.CompletedTask

type LogEffect(logger: ILogger, message: string) =
    interface IActorEffect with
        member _.ExecuteAsync(context: ActorCapabilityContext, _: CancellationToken) : ValueTask =
            logger.LogInformation("Document {DocumentId}: {Message}", context.ActorId.Value, message)
            ValueTask.CompletedTask

type StartSettlementEffect(workflowClient: IDaprWorkflowClient, logger: ILogger) =
    static member InstanceIdFor(actorType: string, documentId: string) : string =
        $"settlement-{actorType}-{documentId}"

    interface IActorEffect with
        member _.ExecuteAsync(context: ActorCapabilityContext, cancellationToken: CancellationToken) : ValueTask =
            let bag = CapabilityContext.State(context)
            let documentId = context.ActorId.Value
            let documentType =
                match bag.Get<string>("documentType") with
                | null -> context.ActorType
                | t -> t
            let requester =
                match bag.Get<string>("requester") with
                | null -> String.Empty
                | r -> r
            let amount = bag.Get<decimal>("amount")
            let parties =
                match bag.Get<string[]>("parties") with
                | null -> [||]
                | p -> p
            let simulateChargeFailure = bag.Get<bool>("simulateChargeFailure")

            let input : SettlementInput = {
                DocumentId = documentId
                DocumentType = documentType
                Requester = requester
                Amount = amount
                Parties = (parties :> IReadOnlyList<string>)
                SimulateChargeFailure = simulateChargeFailure
            }

            let instanceId = StartSettlementEffect.InstanceIdFor(context.ActorType, documentId)
            logger.LogInformation("Document {DocumentId} approved; starting settlement workflow {InstanceId}.", documentId, instanceId)

            let workflowName = "SettlementWorkflow"
            ValueTask(
                (task {
                    let! _ = workflowClient.ScheduleNewWorkflowAsync(workflowName, instanceId, input)
                    return ()
                }) :> Task)
