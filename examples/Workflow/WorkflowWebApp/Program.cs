using System.Text.Json.Serialization;
using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;
using WorkflowWebApp.Activities;
using WorkflowWebApp.Workflows;
using WorkflowWebApp.Models;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using Dapr.Client;

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
    options.RegisterActivity<UpdateInventoryActivity>();
});

WebApplication app = builder.Build();


// POST starts new order workflow instance
app.MapPost("/orders", [Obsolete] async (DaprClient client, [FromBody] OrderPayload orderInfo) =>
{
    // Generate a unique ID for the workflow
    string orderId = Guid.NewGuid().ToString()[..8];
    // All the necessary inputs (with workflow options being optional)
    string workflowComponent = "dapr";
    string workflowName = "OrderProcessingWorkflow";
    object input = orderInfo;
    Dictionary<string, string> workflowOptions = new Dictionary<string, string>();
    CancellationToken cts = new CancellationToken();

    if (orderInfo?.Name == null)
    {
        return Results.BadRequest(new
        {
            message = "Order data was missing from the request",
            example = new OrderPayload("Paperclips", 99.95),
        });
    }

    // Start the workflow
    var response = await client.StartWorkflowAsync(orderId, workflowComponent, workflowName, input, workflowOptions, cts);
    // Get information on the workflow
    var state = await client.GetWorkflowAsync(orderId, workflowComponent, workflowName);
    orderId = response.InstanceId;
    // return an HTTP 202 and a Location header to be used for status query
    return Results.AcceptedAtRoute("GetOrderInfoEndpoint", new { orderId });
});

// GET fetches state for order workflow to report status
app.MapGet("/orders/{orderId}", [Obsolete] async (string orderId, DaprClient client) =>
{
    // WorkflowState state = await client.GetWorkflowStateAsync(orderId, true);
    string workflowComponent = "dapr";
    string workflowName = "OrderProcessingWorkflow";

    var state = await client.GetWorkflowAsync(orderId, workflowComponent, workflowName);

    if (state.instanceId == "")
    {
        return Results.NotFound($"No order with ID = '{orderId}' was found.");
    }

    var httpResponsePayload = new
    {
        status = state.metadata["dapr.workflow.runtime_status"].ToString(),
    };

    if (state.metadata["dapr.workflow.runtime_status"].ToString() == "RUNNING")
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


app.MapPost("/reset", [Obsolete] async (DaprClient client) =>
{
    // Make this into a webAPI rather than throwing this into this activity
    // Save a bunch of items in the state store
    await client.SaveStateAsync<OrderPayload>("statestore", "Paperclips",  new OrderPayload(Name: "Paperclips", TotalCost: 99.95, Quantity: 100));

     return Results.Ok();
});

app.Run();