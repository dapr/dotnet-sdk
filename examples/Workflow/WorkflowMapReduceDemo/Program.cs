
using System.Diagnostics;
using Dapr.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkflowMapReduceDemo.Workflows;
using WorkflowMapReduceDemo.Workflows.Activities;

using var app = Host.CreateDefaultBuilder()
    .ConfigureLogging(b => b.AddConsole())
    .ConfigureServices(services =>
    {
        services.AddDaprWorkflow(opt =>
        {
            opt.RegisterWorkflow<MapReduceWorkflow>();
            opt.RegisterWorkflow<ShardWorkflow>();
            opt.RegisterActivity<MapWorkerActivity>();
        });
    })
    .Build();

await app.StartAsync();
    
await using var scope = app.Services.CreateAsyncScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
var input = new MapReduceInput(
    ShardCount: 200,
    WorkersPerShard: 15,
    WorkerDelayMsBase: 100,
    WorkerDelayMsJitter: 250,
    ShardBatchSize: 20,
    WorkerBatchSize: 10);

var instanceId = Guid.NewGuid().ToString();
logger.LogInformation("Starting workflow with instance ID '{instanceId}'", instanceId);

var sw = Stopwatch.StartNew();
await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(MapReduceWorkflow), instanceId, input);
var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(instanceId, true);
sw.Stop();
Console.WriteLine(result);

var subworkflowCount = input.ShardCount;
var totalOperations = (long)input.ShardCount * input.WorkersPerShard;
var workflowOutput = result.ReadOutputAs<MapReduceResult>();
var totalActivityDelayMs = workflowOutput?.IntentionalDelayMs ?? null;

logger.LogInformation(
    "Workflow run summary: instanceId={InstanceId}, workflow={WorkflowName}, elapsedMs={ElapsedMs}, totalActivityDelayTimeMs={TotalDelayTimeMs} shards(subworkflows)={SubworkflowCount}, totalOperations={TotalOperations}",
    instanceId,
    nameof(MapReduceWorkflow),
    sw.ElapsedMilliseconds,
    totalActivityDelayMs,
    subworkflowCount,
    totalOperations);
