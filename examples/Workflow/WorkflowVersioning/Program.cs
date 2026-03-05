using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using WorkflowVersioning.Services;
using WorkflowVersioning.Workflows.VacationApproval.Activities;
using WorkflowVersioning.Workflows.VacationApproval.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IEmailService, EmailService>();

builder.Services.AddDaprWorkflowVersioning();

// By default, it registers a numerical versioning strategy - let's demonstrate overriding this with a date-based approach
// and non-standard options
const string optionsName = "workflow-defaults";
builder.Services.UseDefaultWorkflowStrategy<NumericVersionStrategy>(optionsName);
builder.Services.ConfigureStrategyOptions<NumericVersionStrategyOptions>(optionsName, o =>
{
    o.SuffixPrefix = "V";
});

builder.Services.AddDaprWorkflow(w =>
{
    w.RegisterActivity<SendEmailActivity>();
});

var app = builder.Build();

app.MapGet("/start/{workflowId}",
    async (DaprWorkflowClient workflowClient, [AsParameters] VacationRequest request, string workflowId) =>
    {
        await workflowClient.ScheduleNewWorkflowAsync("VacationApprovalWorkflow", workflowId, request);
        var a = 0;
        a++;

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
