using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using WorkflowVersioning.Services;
using WorkflowVersioning.Versioning;
using WorkflowVersioning.Workflows.VacationApproval;
using WorkflowVersioning.Workflows.VacationApproval.Activities;
using WorkflowVersioning.Workflows.VacationApproval.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IEmailService, EmailService>();

builder.Services.AddDaprWorkflowVersioning(v =>
{
    v.DefaultStrategy = _ => new NumericalStrategy();
    v.DefaultSelector = _ => new MaxVersionSelector();
});
builder.Services.AddDaprWorkflow(w =>
{
    w.RegisterActivity<SendEmailActivity>();
});

var app = builder.Build();

app.MapGet("/start/{workflowId}",
    async (DaprWorkflowClient workflowClient, [AsParameters] VacationRequest request, string workflowId) =>
    {
        await workflowClient.ScheduleNewWorkflowAsync(nameof(VacationApprovalWorkflow), workflowId, request);
    });

app.MapGet("/approve/{workflowId}", async (DaprWorkflowClient workflowClient, string workflowId) =>
{
    await workflowClient.RaiseEventAsync(workflowId, "Approval", true);
});

app.MapGet("/reject/{workflowId}", async (DaprWorkflowClient workflowClient,  string workflowId) =>
{
    await workflowClient.RaiseEventAsync(workflowId, "Approval", false);
});

await app.RunAsync();
