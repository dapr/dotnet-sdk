using Dapr.Workflow.Versioning;

var builder = WebApplication.CreateBuilder(args);

// Enable workflow versioning and allow the generator to scan referenced assemblies.
builder.Services.AddDaprWorkflowVersioning();

var app = builder.Build();

app.MapGet("/registry", (IServiceProvider services) =>
{
    var registry = GeneratedWorkflowVersionRegistry.GetWorkflowVersionRegistry(services);
    return Results.Ok(registry);
});

await app.RunAsync();
