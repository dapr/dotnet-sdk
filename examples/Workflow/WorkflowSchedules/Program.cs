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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowSchedules.Workflows;

var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options => options.RegisterWorkflow<ReportWorkflow>());
});

// Start the app so the workflow worker is connected before the schedule fires.
using var host = builder.Build();
await host.StartAsync();

await using var scope = host.Services.CreateAsyncScope();
var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

const string scheduleName = "daily-report";

// Start ReportWorkflow every 5 seconds, 3 times. A production schedule would more likely be a cron
// expression with seconds, such as "0 0 9 * * MON-FRI" (09:00 UTC on weekdays).
await client.CreateWorkflowScheduleAsync(scheduleName, nameof(ReportWorkflow), "@every 5s",
    new WorkflowScheduleOptions { Input = "sales", Repeats = 3, Overwrite = true });
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

// Every run's instance ID starts with "<schedule name>_".
var runs = await client.ListInstanceIdsAsync(instanceIdPrefix: $"{scheduleName}_");
Console.WriteLine($"Runs: {runs.InstanceIds.Count}");

await client.DeleteWorkflowScheduleAsync(scheduleName);
Console.WriteLine($"Deleted schedule '{scheduleName}'");
