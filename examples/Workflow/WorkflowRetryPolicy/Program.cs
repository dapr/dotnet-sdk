// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowRetryPolicyExample.Activities;
using WorkflowRetryPolicyExample.Workflows;

var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options =>
    {
        // Register workflows
        options.RegisterWorkflow<RetryPolicyDemo>();
        options.RegisterWorkflow<FlakyChildWorkflow>();

        // Register activities
        options.RegisterActivity<FlakyActivity>();
    });
});

// Start the app - this is the point where we connect to the Dapr sidecar to listen
// for workflow work-items to execute
using var host = builder.Build();
await host.StartAsync();

await using var scope = host.Services.CreateAsyncScope();
var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

var instanceId = $"retry-demo-{Guid.NewGuid().ToString()[..8]}";
Console.WriteLine($"Starting workflow '{instanceId}'...");
Console.WriteLine();

// Start the workflow
await daprWorkflowClient.ScheduleNewWorkflowAsync(
    nameof(RetryPolicyDemo), instanceId, "Hello from retry demo");

// Poll for the workflow to complete
WorkflowState? state;
while (true)
{
    state = await daprWorkflowClient.GetWorkflowStateAsync(instanceId, true);
    Console.WriteLine($"Workflow status: {state?.RuntimeStatus}");

    if (state?.IsWorkflowCompleted == true)
        break;

    await Task.Delay(TimeSpan.FromSeconds(1));
}

Console.WriteLine();

if (state?.RuntimeStatus == WorkflowRuntimeStatus.Completed)
{
    var results = state.ReadOutputAs<string[]>() ?? [];
    Console.WriteLine("Workflow completed successfully! Results:");
    foreach (var result in results)
    {
        Console.WriteLine($"  - {result}");
    }
}
else
{
    Console.WriteLine($"Workflow finished with status: {state?.RuntimeStatus}");
}
