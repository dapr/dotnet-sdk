using Dapr.Workflow;

// The workflow host is a background service that connects to the sidecar over gRPC
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Dapr workflows are registered as part of the service configuration
builder.Services.AddWorkflow(options =>
{
    // Example of registering a "Hello World" workflow function
    options.RegisterWorkflow<string, string>("HelloWorld", implementation: async (context, input) => 
    {
        string result  = "";
        result = await context.CallActivityAsync<string>("SayHello", "World");
        return result;
    });

    // Example of registering a "Say Hello" workflow activity function
    options.RegisterActivity<string, string>("SayHello", implementation: (context, input) =>
    {
        return Task.FromResult($"Hello, {input}!");
    });
});

WebApplication app = builder.Build();

// POST starts new workflow instances
app.MapPost("/workflow", async (HttpContext context, WorkflowClient client) =>
{
    string id = Guid.NewGuid().ToString()[..8];
    await client.ScheduleNewWorkflowAsync("HelloWorld", id);

    // return an HTTP 202 and a Location header to be used for status query
    return Results.AcceptedAtRoute("GetWorkflowEndpoint", new { id });
});

// GET fetches metadata for specific workflow instances
app.MapGet("/workflow/{id}", async (string id, WorkflowClient client) =>
{
    WorkflowMetadata metadata = await client.GetWorkflowMetadataAsync(id, getInputsAndOutputs: true);
    if (metadata.Exists)
    {
        return Results.Ok(metadata);
    }
    else
    {
        return Results.NotFound($"No workflow with ID = '{id}' was found.");
    }
}).WithName("GetWorkflowEndpoint");

app.Run("http://0.0.0.0:8080");

