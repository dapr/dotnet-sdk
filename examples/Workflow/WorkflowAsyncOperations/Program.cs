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
using WorkflowAsyncOperations.Activities;
using WorkflowAsyncOperations.Models;
using WorkflowAsyncOperations.Workflows;

var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options =>
    {
        options.RegisterWorkflow<DemoWorkflow>();
        options.RegisterActivity<ProcessPaymentActivity>();
        options.RegisterActivity<NotifyWarehouseActivity>();
    });
});

var host = await builder.StartAsync();

await using var scope = host.Services.CreateAsyncScope();
var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

var instanceId = $"demo-workflow-{Guid.NewGuid().ToString()[..8]}";
var transaction = new Transaction(16.58m);
await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(DemoWorkflow), instanceId, transaction);

//Poll for status updates every second
var status = await daprWorkflowClient.GetWorkflowStateAsync(instanceId);
do
{
    Console.WriteLine($"Current status: {status.RuntimeStatus}, step: {status.ReadCustomStatusAs<string>()}");
    status = await daprWorkflowClient.GetWorkflowStateAsync(instanceId);
} while (!status.IsWorkflowCompleted);

Console.WriteLine($"Workflow completed - {status.ReadCustomStatusAs<string>()}");
