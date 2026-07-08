using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Examples.Approvals;
using Dapr.Actors.Next.Interpreted;
using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// The settlement workflow and its activities. The workflow is what an approved document hands off to to manage approval lifecycle.
// Workflow and activity registration is performed automatically in Dapr .NET SDK 1.18+, so no need to uncomment and register
// each here at startup - just add `builder.Services.AddDaprWorkflow()`.
builder.Services.AddDaprWorkflow(_ =>
{
    // options.RegisterWorkflow<SettlementWorkflow>();
    // options.RegisterActivity<NotifyPartiesActivity>();
    // options.RegisterActivity<ChargeOrProvisionActivity>();
    // options.RegisterActivity<ReleaseReservationActivity>();
    // options.RegisterActivity<SignalDocumentActivity>();
});

// Adding this to facilitate mocking the `DaprWorkflowClient` in tests more easily and so it can be readily passed into
// project methods instead of the concrete `DaprWorkflowClient`. While `DaprWorkflowClient` is, the `IDaprWorkflowClient` is
// not automatically registered by `builder.Services.AddDaprWorkflow()` at this time.
builder.Services.AddSingleton<IDaprWorkflowClient>(sp => sp.GetRequiredService<DaprWorkflowClient>());

// The interpreted approval runtime. The capability registry (which owns the workflow-starting effect) is
// registered before AddDaprInterpretedActors so it wins over the default empty registry.
builder.Services.AddSingleton<IActorRegistry, ApprovalTypeRegistry>();
builder.Services.AddSingleton<ICapabilityRegistry>(sp =>
    new ApprovalCapabilityRegistry(
        sp.GetRequiredService<IDaprWorkflowClient>(),
        sp.GetRequiredService<ILoggerFactory>()));
builder.Services.AddSingleton<ApprovalControlPlane>();
builder.Services.AddDaprInterpretedActors(ApprovalDefinitions.ActorType);

var app = builder.Build();

app.MapGet("/", () =>
    "Approval routing sample. GET /document-types, POST /documents/{id}/onboard?type=ExpenseReport, then submit, begin-review, and approve.");

// The runtime catalog of on-boardable document types (behavior authored as data, not compiled).
app.MapGet("/document-types", () => ApprovalDefinitions.Catalog);

// What the app actually hosts: one compiled interpreted actor type that runs every document type.
app.MapGet("/hosted-types", (ApprovalControlPlane controlPlane) => controlPlane.HostedActorTypes());

// Onboard a document: deploy its type's definition at runtime (verified before it is stored) and purge
// any state left by a previous run, so the document starts fresh at its initial state. Onboarding is
// therefore both cleanup and setup, which keeps the sample re-runnable without a separate reset step.
app.MapPost("/documents/{documentId}/onboard", async (
    string documentId,
    string type,
    [FromServices] InterpretedMachineDeployer deployer,
    [FromServices] ApprovalControlPlane controlPlane,
    CancellationToken cancellationToken) =>
{
    var definition = ApprovalDefinitions.ForType(type);
    await deployer.DeployAsync(ApprovalDefinitions.ActorType, ActorId.Create(documentId), definition, cancellationToken);
    await controlPlane.ResetAsync(documentId, cancellationToken);
    return Results.Ok(new { DocumentId = documentId, DocumentType = type, State = definition.InitialState });
});

app.MapPost("/documents/{documentId}/submit", (
    string documentId,
    SubmitDocument submission,
    [FromServices] ApprovalControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.SubmitAsync(documentId, submission, cancellationToken)));

app.MapPost("/documents/{documentId}/begin-review", (
    string documentId,
    [FromServices] ApprovalControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.BeginReviewAsync(documentId, cancellationToken)));

app.MapPost("/documents/{documentId}/begin-legal-review", (
    string documentId,
    [FromServices] ApprovalControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.BeginLegalReviewAsync(documentId, cancellationToken)));

app.MapPost("/documents/{documentId}/complete-legal-review", (
    string documentId,
    [FromServices] ApprovalControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.CompleteLegalReviewAsync(documentId, cancellationToken)));

app.MapPost("/documents/{documentId}/approve", (
    string documentId,
    Decision decision,
    [FromServices] ApprovalControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.ApproveAsync(documentId, decision, cancellationToken)));

app.MapPost("/documents/{documentId}/reject", (
    string documentId,
    Decision decision,
    [FromServices] ApprovalControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.RejectAsync(documentId, decision, cancellationToken)));

await app.RunAsync();
return;

static async Task<IResult> ToJsonResult(Task<string?> invoke)
{
    var json = await invoke;
    return json is null ? Results.NoContent() : Results.Content(json, "application/json");
}
