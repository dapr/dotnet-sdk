/*
 * Copyright 2026 The Dapr Authors
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
limitations under the License.
*/

using Dapr.Workflow;
using Dapr.Workflow.Client;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowSchedules.Workflows;

var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options => options.RegisterWorkflow<ReportWorkflow>());
});

using var host = builder.Build();
await host.StartAsync();

await using var scope = host.Services.CreateAsyncScope();
var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

const string scheduleName = "daily-report";

// Start ReportWorkflow every 5 seconds, 3 times. A production schedule would more likely be a cron
// expression with seconds, such as "0 0 9 * * MON-FRI" (09:00 UTC on weekdays).
// StartAsync returns before the workflow worker has connected, and creating a schedule needs a connected
// worker, so retry until the runtime accepts it.
for (var attempt = 1; ; attempt++)
{
    try
    {
        await client.CreateWorkflowScheduleAsync(scheduleName, nameof(ReportWorkflow), "@every 5s",
            new WorkflowScheduleOptions { Input = "sales", Repeats = 3, Overwrite = true });
        break;
    }
    catch (RpcException e) when (e.StatusCode == StatusCode.FailedPrecondition && attempt < 30)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}
Console.WriteLine($"Created schedule '{scheduleName}'");

// Start an extra run now, without moving the next scheduled run.
await client.TriggerWorkflowScheduleAsync(scheduleName);
Console.WriteLine("Triggered a manual run");

WorkflowSchedule? schedule;
do
{
    await Task.Delay(TimeSpan.FromSeconds(2));
    schedule = await client.GetWorkflowScheduleAsync(scheduleName);
} while (schedule?.Status?.State == WorkflowScheduleState.Active);
Console.WriteLine($"Schedule state: {schedule?.Status?.State}, runs: {schedule?.Status?.RunCount}");

// Every run's instance ID starts with "<schedule name>_". A page can come back short, even empty, while
// more remain, so follow the continuation token to the end.
var runIds = new List<string>();
string? continuationToken = null;
do
{
    var page = await client.ListInstanceIdsAsync(continuationToken, null, $"{scheduleName}_");
    runIds.AddRange(page.InstanceIds);
    continuationToken = page.ContinuationToken;
} while (continuationToken is not null);

// The schedule completes when its last run starts, not when that run finishes.
foreach (var runId in runIds)
{
    await client.WaitForWorkflowCompletionAsync(runId);
}
Console.WriteLine($"Runs: {runIds.Count}");

await client.DeleteWorkflowScheduleAsync(scheduleName);
Console.WriteLine($"Deleted schedule '{scheduleName}'");

// Deleting a schedule leaves its runs, so purge them.
foreach (var runId in runIds)
{
    await client.PurgeInstanceAsync(runId);
}
