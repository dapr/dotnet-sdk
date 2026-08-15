#nowarn "FS3261"
open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Dapr.Workflow
open Dapr.Workflow.Versioning
open WorkflowVersioning.FSharp.Services
open WorkflowVersioning.FSharp.Workflows.VacationApproval
open WorkflowVersioning.FSharp.Workflows.VacationApproval.Activities
open WorkflowVersioning.FSharp.Workflows.VacationApproval.Models

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()

builder.Services.AddSingleton<IEmailService, EmailService>() |> ignore
builder.Services.AddDaprWorkflowVersioning() |> ignore

let optionsName = "workflow-defaults"
builder.Services.UseDefaultWorkflowStrategy<NumericVersionStrategy>(optionsName) |> ignore
builder.Services.ConfigureStrategyOptions<NumericVersionStrategyOptions>(
    optionsName,
    Action<NumericVersionStrategyOptions>(fun o -> o.SuffixPrefix <- "V")) |> ignore

builder.Services.AddDaprWorkflow(Action<WorkflowRuntimeOptions>(fun w ->
    w.RegisterActivity<SendEmailActivity>())) |> ignore

let app: WebApplication = builder.Build()

app.MapGet("/start/{workflowId}",
    Func<DaprWorkflowClient, string, HttpContext, Task<IResult>>(fun workflowClient workflowId ctx ->
        task {
            let query = ctx.Request.Query
            let request : VacationRequest = {
                EmployeeName = query.["name"].ToString()
                StartDate = DateOnly.Parse(query.["start"].ToString())
                EndDate = DateOnly.Parse(query.["end"].ToString())
            }
            let! _ = workflowClient.ScheduleNewWorkflowAsync("VacationApprovalWorkflow", workflowId, box request)
            return Results.Ok()
        })) |> ignore

app.MapGet("/approve/{workflowId}",
    Func<DaprWorkflowClient, string, Task<IResult>>(fun workflowClient workflowId ->
        task {
            do! workflowClient.RaiseEventAsync(workflowId, "Approval", box true)
            return Results.Ok()
        })) |> ignore

app.MapGet("/reject/{workflowId}",
    Func<DaprWorkflowClient, string, Task<IResult>>(fun workflowClient workflowId ->
        task {
            do! workflowClient.RaiseEventAsync(workflowId, "Approval", box false)
            return Results.Ok()
        })) |> ignore

app.Run()