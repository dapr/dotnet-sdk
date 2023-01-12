using Dapr.Workflow;

// The workflow host is a background service that connects to the sidecar over gRPC
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Dapr workflows are registered as part of the service configuration
builder.Services.AddDaprWorkflow(options =>
{
    // Example of registering a "PlaceOrder" workflow function
    options.RegisterWorkflow<string, string>("PlaceOrder", implementation: async (context, input) =>
    {
        // In real life there are other steps related to placing an order, like reserving
        // inventory and charging the customer credit card etc. But let's keep it simple ;)
        return await context.CallActivityAsync<string>("ShipProduct", "Coffee Beans");
    });

    // Example of registering a "ShipProduct" workflow activity function
    options.RegisterActivity<string, string>("ShipProduct", implementation: (context, input) =>
    {
        return Task.FromResult($"We are shipping {input} to the customer using our hoard of drones!");
    });
});

WebApplication app = builder.Build();

// POST starts new workflow instances
app.MapPost("/order", async (HttpContext context, WorkflowClient client) =>
{
    string id = Guid.NewGuid().ToString()[..8];
    await client.ScheduleNewWorkflowAsync("PlaceOrder", id);

    // return an HTTP 202 and a Location header to be used for status query
    return Results.AcceptedAtRoute("GetOrderEndpoint", new { id });
});

// GET fetches metadata for specific order workflow instances
app.MapGet("/order/{id}", async (string id, WorkflowClient client) =>
{
    WorkflowMetadata metadata = await client.GetWorkflowMetadataAsync(id, getInputsAndOutputs: true);
    if (metadata.Exists)
    {
        return Results.Ok(metadata);
    }
    else
    {
        return Results.NotFound($"No workflow created for order with ID = '{id}' was found.");
    }
}).WithName("GetOrderEndpoint");

app.Run("http://0.0.0.0:10080");

