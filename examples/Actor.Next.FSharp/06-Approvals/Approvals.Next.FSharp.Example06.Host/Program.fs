open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Abstractions.Registry
open Dapr.Actors.Next.Interpreted
open Dapr.Workflow
open Dapr.Actors.Next.Examples.Approvals

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()

builder.Services.AddDaprWorkflow(Action<WorkflowRuntimeOptions>(fun _ -> ())) |> ignore
builder.Services.AddSingleton<IDaprWorkflowClient>(fun sp -> sp.GetRequiredService<DaprWorkflowClient>() :> IDaprWorkflowClient) |> ignore

builder.Services.AddSingleton<IActorRegistry, ApprovalTypeRegistry>() |> ignore
builder.Services.AddSingleton<ICapabilityRegistry>(fun sp ->
    ApprovalCapabilityRegistry(
        sp.GetRequiredService<IDaprWorkflowClient>(),
        sp.GetRequiredService<ILoggerFactory>()) :> ICapabilityRegistry) |> ignore
builder.Services.AddSingleton<ApprovalControlPlane>() |> ignore
builder.Services.AddDaprInterpretedActors(ApprovalDefinitions.ActorType) |> ignore

let app: WebApplication = builder.Build()

app.MapGet("/", Func<string>(fun () ->
    "Approval routing sample. GET /document-types, POST /documents/{id}/onboard?type=ExpenseReport, then submit, begin-review, and approve.")) |> ignore

app.MapGet("/document-types", Func<IReadOnlyList<DocumentTypeCard>>(fun () ->
    ApprovalDefinitions.Catalog)) |> ignore

app.MapGet("/hosted-types", Func<ApprovalControlPlane, IReadOnlyList<string>>(fun cp ->
    cp.HostedActorTypes())) |> ignore

let toJsonResult (invoke: Task<string>) : Task<IResult> =
    task {
        let! json = invoke
        return if isNull json then Results.NoContent() else Results.Content(json, "application/json")
    }

app.MapPost("/documents/{documentId}/onboard",
    Func<string, string, InterpretedMachineDeployer, ApprovalControlPlane, CancellationToken, Task<IResult>>(
        fun documentId documentType deployer controlPlane ct ->
            task {
                let definition = ApprovalDefinitions.ForType(documentType)
                do! deployer.DeployAsync(ApprovalDefinitions.ActorType, ActorId.Create(documentId), definition, ct)
                do! controlPlane.ResetAsync(documentId, ct)
                return Results.Ok({| DocumentId = documentId; DocumentType = documentType; State = definition.InitialState |})
            })) |> ignore

app.MapPost("/documents/{documentId}/submit",
    Func<string, SubmitDocument, ApprovalControlPlane, CancellationToken, Task<IResult>>(
        fun documentId submission controlPlane ct ->
            toJsonResult (controlPlane.SubmitAsync(documentId, submission, ct)))) |> ignore

app.MapPost("/documents/{documentId}/begin-review",
    Func<string, ApprovalControlPlane, CancellationToken, Task<IResult>>(
        fun documentId controlPlane ct ->
            toJsonResult (controlPlane.BeginReviewAsync(documentId, ct)))) |> ignore

app.MapPost("/documents/{documentId}/begin-legal-review",
    Func<string, ApprovalControlPlane, CancellationToken, Task<IResult>>(
        fun documentId controlPlane ct ->
            toJsonResult (controlPlane.BeginLegalReviewAsync(documentId, ct)))) |> ignore

app.MapPost("/documents/{documentId}/complete-legal-review",
    Func<string, ApprovalControlPlane, CancellationToken, Task<IResult>>(
        fun documentId controlPlane ct ->
            toJsonResult (controlPlane.CompleteLegalReviewAsync(documentId, ct)))) |> ignore

app.MapPost("/documents/{documentId}/approve",
    Func<string, Decision, ApprovalControlPlane, CancellationToken, Task<IResult>>(
        fun documentId decision controlPlane ct ->
            toJsonResult (controlPlane.ApproveAsync(documentId, decision, ct)))) |> ignore

app.MapPost("/documents/{documentId}/reject",
    Func<string, Decision, ApprovalControlPlane, CancellationToken, Task<IResult>>(
        fun documentId decision controlPlane ct ->
            toJsonResult (controlPlane.RejectAsync(documentId, decision, ct)))) |> ignore

app.Run()
