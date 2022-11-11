using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System.Web;
using Microsoft.AspNetCore.Http;
using Dapr.Workflow;

// The workflow host is a background service that connects to the sidecar over gRPC
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddWorkflow(options =>
{
    options.RegisterWorkflow<string, string>("HelloWorld", implementation: async (context, input) => 
    {
        string result  = "";
        result = await context.CallActivityAsync<string>("SayHello", "World");
        return result;
    });
    options.RegisterActivity<string, string>("SayHello", implementation: async (context, input) => await Task.FromResult($"Hello, {input}!"));
});

WebApplication app = builder.Build();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapPost("/workflow", Schedule);

    endpoints.MapGet("/workflow/{id}", GetWorkflow);
});

async Task<IResult> Schedule(HttpContext context)
{
    var client = context.RequestServices.GetRequiredService<WorkflowClient>();
    string id = Guid.NewGuid().ToString()[..8];
    await client.ScheduleNewWorkflowAsync("HelloWorld", id);
    return Results.Ok(id);
}

async Task<IResult> GetWorkflow(HttpContext context)
{
    var id = (string)context.Request.RouteValues["id"];
    var client = context.RequestServices.GetRequiredService<WorkflowClient>();
    WorkflowMetadata metadata = await client.GetWorkflowMetadataAsync(id, getInputsAndOutputs: true);
    if (metadata.Exists)
    {
        Console.WriteLine($"Created workflow id: '{id}'");
        Console.WriteLine($"Metadata:  {metadata.Details}");
        return Results.Ok(metadata);
    }
    else
    {
        return Results.NotFound($"No workflow with ID = '{id}' was found.");
    }
}

app.Run("http://0.0.0.0:8080");