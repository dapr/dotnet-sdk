using System.Text.Json.Serialization;
using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;
using WorkflowWebApp.Activities;
using WorkflowWebApp.Workflows;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

// The workflow host is a background service that connects to the sidecar over gRPC
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure HTTP JSON options.
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Dapr workflows are registered as part of the service configuration
builder.Services.AddDaprWorkflow(options =>
{
    // Note that it's also possible to register a lambda function as the workflow
    // or activity implementation instead of a class.
    options.RegisterWorkflow<OrderProcessingWorkflow>();

    // These are the activities that get invoked by the workflow(s).
    options.RegisterActivity<NotifyActivity>();
    options.RegisterActivity<ReserveInventoryActivity>();
    options.RegisterActivity<ProcessPaymentActivity>();
});

WebApplication app = builder.Build();

// POST starts new order workflow instance
app.MapPost("/orders", async (WorkflowEngineClient client, [FromBody] OrderPayload orderInfo) =>
{
    if (orderInfo?.Name == null)
    {
        return Results.BadRequest(new
        {
            message = "Order data was missing from the request",
            example = new OrderPayload("Paperclips", 99.95),
        });
    }

    // Randomly generated order ID that is 8 characters long.
    string orderId = Guid.NewGuid().ToString()[..8];
    await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), orderId, orderInfo);

    // return an HTTP 202 and a Location header to be used for status query
    return Results.AcceptedAtRoute("GetOrderInfoEndpoint", new { orderId });
});

// GET fetches state for order workflow to report status
app.MapGet("/orders/{orderId}", async (string orderId, WorkflowEngineClient client) =>
{
    WorkflowState state = await client.GetWorkflowStateAsync(orderId, true);
    if (!state.Exists)
    {
        return Results.NotFound($"No order with ID = '{orderId}' was found.");
    }

    var httpResponsePayload = new
    {
        details = state.ReadInputAs<OrderPayload>(),
        status = state.RuntimeStatus.ToString(),
        result = state.ReadOutputAs<OrderResult>(),
    };

    if (state.IsWorkflowRunning)
    {
        // HTTP 202 Accepted - async polling clients should keep polling for status
        return Results.AcceptedAtRoute("GetOrderInfoEndpoint", new { orderId }, httpResponsePayload);
    }
    else
    {
        // HTTP 200 OK
        return Results.Ok(httpResponsePayload);
    }
}).WithName("GetOrderInfoEndpoint");

app.Run();

