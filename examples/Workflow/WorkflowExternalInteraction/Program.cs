using Dapr.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowExternalInteraction.Activities;
using WorkflowExternalInteraction.Workflows;

var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options =>
    {
        options.RegisterWorkflow<DemoWorkflow>();
        options.RegisterActivity<ApproveActivity>();
        options.RegisterActivity<RejectActivity>();
    });
});

using var host = builder.Build();
await host.StartAsync();

await using var scope = host.Services.CreateAsyncScope();
var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

var instanceId = $"demo-workflow-{Guid.NewGuid().ToString()[..8]}";

await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(DemoWorkflow), instanceId, instanceId);


bool enterPressed = false;
Console.WriteLine("Press [ENTER] within the next 10 seconds to approve this workflow");
using (var cts = new CancellationTokenSource())
{
    var inputTask = Task.Run(() =>
    {
        if (Console.ReadKey().Key == ConsoleKey.Enter)
        {
            Console.WriteLine("Approved");
            enterPressed = true;
            cts.Cancel(); //Cancel the delay task if Enter is pressed
        }
    });

    try
    {
        await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
    }
    catch (TaskCanceledException)
    {
        // Task was cancelled because Enter was pressed
    }
}

if (enterPressed)
{
    await daprWorkflowClient.RaiseEventAsync(instanceId, "Approval", true);
}
else
{
    Console.WriteLine("Rejected");
}

await daprWorkflowClient.WaitForWorkflowCompletionAsync(instanceId);
var state = await daprWorkflowClient.GetWorkflowStateAsync(instanceId);
Console.WriteLine($"Workflow state: {state.RuntimeStatus}");
