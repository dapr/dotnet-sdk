using System.Text.Json.Serialization;
using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

// The workflow host is a background service that connects to the sidecar over gRPC
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddWorkflow(options =>
{
    options.RegisterWorkflow<string, string>("helloWorld", implementation: async (context, input) => 
    {
        await context.CallActivityAsync<string>("SayHello", "Hello World");
    });
    options.RegisterActivity<string, string>("SayHello", (context, message) => $"{message}!");
});