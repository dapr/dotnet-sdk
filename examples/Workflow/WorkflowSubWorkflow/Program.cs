using Dapr.Client;
using Dapr.Workflow;
using WorkflowSubWorkflow.Workflows;
using Microsoft.Extensions.Hosting;

const string DaprWorkflowComponent = "dapr";

// The workflow host is a background service that connects to the sidecar over gRPC
var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options =>
    {
        options.RegisterWorkflow<DemoWorkflow>();
        options.RegisterWorkflow<DemoSubWorkflow>();
    });
});

// Dapr uses a random port for gRPC by default. If we don't know what that port
// is (because this app was started separate from dapr), then assume 4001.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DAPR_GRPC_PORT")))
{
    Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", "4001");
}

// Start the app - this is the point where we connect to the Dapr sidecar to
// listen for workflow work-items to execute.
using var host = builder.Build();
host.Start();


DaprClient daprClient = new DaprClientBuilder().Build();

while (!await daprClient.CheckHealthAsync())
{
    Thread.Sleep(TimeSpan.FromSeconds(5));
}


using (daprClient)
{
    Console.WriteLine($"Workflow Started.");
    await daprClient.WaitForSidecarAsync();

    string instanceId = $"demo-workflow-{Guid.NewGuid().ToString()[..8]}";

    await daprClient.StartWorkflowAsync(
    workflowComponent: DaprWorkflowComponent,
    workflowName: nameof(DemoWorkflow),
    instanceId: instanceId,
    input: instanceId);

    await daprClient.WaitForWorkflowCompletionAsync(
        workflowComponent: DaprWorkflowComponent,
        instanceId: instanceId);

    GetWorkflowResponse state = await daprClient.GetWorkflowAsync(
    instanceId: instanceId,
    workflowComponent: DaprWorkflowComponent);
    Console.WriteLine($"Workflow {instanceId} state: {state.RuntimeStatus}");

    state = await daprClient.GetWorkflowAsync(
    instanceId: instanceId + "-sub",
    workflowComponent: DaprWorkflowComponent);
    Console.WriteLine($"Workflow {instanceId} state: {state.RuntimeStatus}");

}