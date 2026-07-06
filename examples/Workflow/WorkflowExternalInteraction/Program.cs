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

Console.WriteLine("Press [ENTER] within the next 10 seconds to approve this workflow");
if (await WaitForEnterAsync(TimeSpan.FromSeconds(10)))
{
    Console.WriteLine("Approved");
    await daprWorkflowClient.RaiseEventAsync(instanceId, "Approval", true);
}
else
{
    Console.WriteLine("Rejected");
}

await daprWorkflowClient.WaitForWorkflowCompletionAsync(instanceId);
var state = await daprWorkflowClient.GetWorkflowStateAsync(instanceId);
Console.WriteLine($"Workflow state: {state?.RuntimeStatus}");
return;

static async Task<bool> WaitForEnterAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
{
    var deadline = DateTime.UtcNow + timeout;
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
    while (DateTime.UtcNow < deadline)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ;

        while (Console.KeyAvailable) // Drain buffered keys
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                return true;
        }
        
        // Wait a bit before checking against
        await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
    }

    return false;
}
