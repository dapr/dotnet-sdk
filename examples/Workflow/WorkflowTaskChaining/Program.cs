// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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
using WorkflowTaskChaining.Activities;
using WorkflowTaskChaining.Workflows;

var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options =>
    {
        options.RegisterWorkflow<DemoWorkflow>();
        options.RegisterActivity<Step1>();
        options.RegisterActivity<Step2>();
        options.RegisterActivity<Step3>();
    });
});

// Start the app - this is the point where we connect to the Dapr sidecar to listen 
// for workflow work-items to execute
using var host = builder.Build();
await host.StartAsync();

await using var scope = host.Services.CreateAsyncScope();
var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
    
//Check health
const int wfInput = 42;
Console.WriteLine(@"Workflow Started");

var instanceId = $"demo-workflow-{Guid.NewGuid().ToString()[..8]}";

//Start the workflow immediately
await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(DemoWorkflow), instanceId, wfInput);

//Get the status of the workflow
WorkflowState workflowState;
while (true)
{
    workflowState = await daprWorkflowClient.GetWorkflowStateAsync(instanceId, true);
    Console.WriteLine($@"Workflow status: {workflowState.RuntimeStatus}");
    if (workflowState.IsWorkflowCompleted)
        break;

    await Task.Delay(TimeSpan.FromSeconds(1));
}

//Display the result from the workflow
var result = string.Join(" ", workflowState.ReadOutputAs<int[]>() ?? Array.Empty<int>());
Console.WriteLine($@"Workflow result: {result}");


