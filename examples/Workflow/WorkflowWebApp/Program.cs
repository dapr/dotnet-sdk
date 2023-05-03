
using System.Text.Json.Serialization;
using Dapr.Client;
using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;
using WorkflowConsoleApp.Activities;
using WorkflowConsoleApp.Models;
using WorkflowConsoleApp.Workflows;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

const string storeName = "statestore";

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
// Dapr uses a random port for gRPC by default. If we don't know what that port
// is (because this app was started separate from dapr), then assume 4001.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DAPR_GRPC_PORT")))
{
    Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", "4001");
}
WebApplication app = builder.Build();
using var daprClient = new DaprClientBuilder().Build();

// POST starts new order workflow instance
app.MapPost("/orders", async (WorkflowEngineClient client, [FromBody] OrderPayload orderInfo) =>
{

    // Wait for the sidecar to become available
    while (!await daprClient.CheckHealthAsync())
    {
        Thread.Sleep(TimeSpan.FromSeconds(5));
    }
    //Restock inventory
    var baseInventory = new List<InventoryItem>
    {
        new InventoryItem(Name: "Paperclips", PerItemCost: 5, Quantity: 100),
        new InventoryItem(Name: "Cars", PerItemCost: 15000, Quantity: 100),
        new InventoryItem(Name: "Computers", PerItemCost: 500, Quantity: 100),
    };
    // Populate the store with items
    foreach (var item in baseInventory)
    {
        Console.WriteLine($"*** \t{item.Name}: {item.Quantity}");
        await daprClient.SaveStateAsync(storeName, item.Name.ToLowerInvariant(), item);
    }

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

    //Restock inventory
    var baseInventory = new List<InventoryItem>
    {
        new InventoryItem(Name: "Paperclips", PerItemCost: 5, Quantity: 100),
        new InventoryItem(Name: "Cars", PerItemCost: 15000, Quantity: 100),
        new InventoryItem(Name: "Computers", PerItemCost: 500, Quantity: 100),
    };
    // Populate the store with items
    foreach (var item in baseInventory)
    {
        Console.WriteLine($"*** \t{item.Name}: {item.Quantity}");
        await daprClient.SaveStateAsync(storeName, item.Name.ToLowerInvariant(), item);
    }


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