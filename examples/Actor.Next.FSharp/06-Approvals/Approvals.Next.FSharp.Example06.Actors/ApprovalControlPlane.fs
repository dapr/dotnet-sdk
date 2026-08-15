namespace Dapr.Actors.Next.Examples.Approvals

open System
open System.Collections.Generic
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions.Registry
open Dapr.Actors.Next.Interpreted

type ApprovalControlPlane(registry: IActorRegistry, client: IDynamicActorClient) =

    member _.HostedActorTypes() : IReadOnlyList<string> =
        registry.Actors
        |> Seq.map (fun a -> a.ActorType)
        |> Seq.sort
        |> Seq.toArray
        :> IReadOnlyList<string>

    member this.SubmitAsync(documentId: string, submission: SubmitDocument, cancellationToken: CancellationToken) : Task<string> =
        this.RaiseAsync(documentId, "Submit", submission, cancellationToken)

    member this.BeginReviewAsync(documentId: string, cancellationToken: CancellationToken) : Task<string> =
        this.RaiseAsync(documentId, "BeginReview", obj(), cancellationToken)

    member this.BeginLegalReviewAsync(documentId: string, cancellationToken: CancellationToken) : Task<string> =
        this.RaiseAsync(documentId, "BeginLegalReview", obj(), cancellationToken)

    member this.CompleteLegalReviewAsync(documentId: string, cancellationToken: CancellationToken) : Task<string> =
        this.RaiseAsync(documentId, "CompleteLegalReview", obj(), cancellationToken)

    member this.ApproveAsync(documentId: string, decision: Decision, cancellationToken: CancellationToken) : Task<string> =
        this.RaiseAsync(documentId, "Approve", decision, cancellationToken)

    member this.RejectAsync(documentId: string, decision: Decision, cancellationToken: CancellationToken) : Task<string> =
        this.RaiseAsync(documentId, "Reject", decision, cancellationToken)

    member _.ResetAsync(documentId: string, cancellationToken: CancellationToken) : Task =
        client.InvokeAsync(ApprovalDefinitions.ActorType, documentId, "Reset", "{}", cancellationToken) :> Task

    member private _.RaiseAsync<'TPayload>(documentId: string, eventName: string, payload: 'TPayload, cancellationToken: CancellationToken) : Task<string> =
        let evt = InterpretedEvent(eventName, JsonSerializer.SerializeToElement(payload))
        client.InvokeAsync(ApprovalDefinitions.ActorType, documentId, "Raise", JsonSerializer.Serialize(evt), cancellationToken)
