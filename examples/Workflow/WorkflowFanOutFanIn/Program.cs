using Dapr.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowFanOutFanIn.Activities;
using WorkflowFanOutFanIn.Workflows;

var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options =>
    {
        options.RegisterWorkflow<DemoWorkflow>();
        options.RegisterActivity<NotifyActivity>();
    });
});

var host = builder.Build();
await host.StartAsync();

await using var scope = host.Services.CreateAsyncScope();
var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

var instanceId = $"workflow-demo-{Guid.NewGuid().ToString()[..8]}";
await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(DemoWorkflow), instanceId, "test input");

await daprWorkflowClient.WaitForWorkflowCompletionAsync(instanceId);
var state = await daprWorkflowClient.GetWorkflowStateAsync(instanceId);
Console.WriteLine($"Workflow state: {state.RuntimeStatus}");
