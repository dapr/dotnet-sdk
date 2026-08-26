namespace Dapr.Actors.Next.Examples.Approvals

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Dapr.Actors.Next.Abstractions.Registry
open Dapr.Actors.Next.Interpreted
open Dapr.Workflow

type NotifyPartiesActivity(logger: ILogger<NotifyPartiesActivity>) =
    inherit WorkflowActivity<PartyNotification, obj>()
    override _.RunAsync(_: WorkflowActivityContext, input: PartyNotification) : Task<obj> =
        logger.LogInformation("Notifying {Party} about approved document {DocumentId}", input.Party, input.DocumentId)
        Task.FromResult<obj>(null)

type ReleaseReservationActivity(logger: ILogger<ReleaseReservationActivity>) =
    inherit WorkflowActivity<ReleaseRequest, obj>()
    override _.RunAsync(_: WorkflowActivityContext, input: ReleaseRequest) : Task<obj> =
        logger.LogInformation("Released reservation of {Amount:C} for document {DocumentId}", input.Amount, input.DocumentId)
        Task.FromResult<obj>(null)

type ChargeOrProvisionActivity(logger: ILogger<ChargeOrProvisionActivity>) =
    inherit WorkflowActivity<ChargeRequest, obj>()
    override _.RunAsync(_: WorkflowActivityContext, input: ChargeRequest) : Task<obj> =
        if input.SimulateFailure then
            logger.LogWarning("Charge for {DocumentId} failed (simulated)", input.DocumentId)
            raise (InvalidOperationException($"Charge for document '{input.DocumentId}' was declined"))
        logger.LogInformation("Charged {Amount:C} for document {DocumentId}", input.Amount, input.DocumentId)
        Task.FromResult<obj>(null)

type SignalDocumentActivity(client: IDynamicActorClient, logger: ILogger<SignalDocumentActivity>) =
    inherit WorkflowActivity<DocumentSignal, obj>()
    override _.RunAsync(_: WorkflowActivityContext, input: DocumentSignal) : Task<obj> =
        task {
            logger.LogInformation("Signalling {EventName} to document {DocumentId}", input.EventName, input.DocumentId)
            let payload = box {| |}
            let evt = InterpretedEvent(input.EventName, JsonSerializer.SerializeToElement(box {| |}))
            let! _ = client.InvokeAsync(ApprovalDefinitions.ActorType, input.DocumentId, "Raise", JsonSerializer.Serialize(evt))
            return null
        }

type SettlementWorkflow() =
    inherit Workflow<SettlementInput, SettlementResult>()

    static member val ChargeRetry : WorkflowTaskOptions =
        WorkflowTaskOptions(
            WorkflowRetryPolicy(
                maxNumberOfAttempts = 4,
                firstRetryInterval = TimeSpan.FromSeconds(2.0),
                backoffCoefficient = 2.0,
                maxRetryInterval = TimeSpan.FromSeconds(30.0)))
        with get

    override _.RunAsync(context: WorkflowContext, input: SettlementInput) : Task<SettlementResult> =
        task {
            let logger = context.CreateReplaySafeLogger<SettlementWorkflow>()

            let notifications =
                input.Parties
                |> Seq.map (fun party ->
                    context.CallActivityAsync(
                        typeof<NotifyPartiesActivity>.Name,
                        box { DocumentId = input.DocumentId; Party = party }))
                |> List.ofSeq
            do! Task.WhenAll(notifications)

            let mutable failed = false
            try
                do! context.CallActivityAsync(
                    typeof<ChargeOrProvisionActivity>.Name,
                    box { DocumentId = input.DocumentId; Amount = input.Amount; SimulateFailure = input.SimulateChargeFailure },
                    SettlementWorkflow.ChargeRetry)
            with :? WorkflowTaskFailedException as ex ->
                logger.LogError("Settlement for {DocumentId} failed: {Reason}. Compensating.", input.DocumentId, ex.FailureDetails.ErrorMessage)
                do! context.CallActivityAsync(
                    typeof<ReleaseReservationActivity>.Name,
                    box { DocumentId = input.DocumentId; Amount = input.Amount })
                do! context.CallActivityAsync(
                    typeof<SignalDocumentActivity>.Name,
                    box { DocumentId = input.DocumentId; EventName = "SettlementFailed" })
                failed <- true

            if failed then
                return { Settled = false; FinalState = "SettlementFailed" }
            else
                do! context.CallActivityAsync(
                    typeof<SignalDocumentActivity>.Name,
                    box { DocumentId = input.DocumentId; EventName = "SettlementCompleted" })
                return { Settled = true; FinalState = "Archived" }
        }
