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

#pragma warning disable CS0618 // Type or member is obsolete
using System.Text;
using Dapr.Jobs;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprJobsClient();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

//Set a handler to deal with incoming jobs
app.MapDaprScheduledJobHandler(async (string jobName, ReadOnlyMemory<byte> jobPayload, ILogger? logger, CancellationToken cancellationToken) =>
{
    logger?.LogInformation("Received trigger invocation for job '{jobName}'", jobName);
    
    var deserializedPayload = Encoding.UTF8.GetString(jobPayload.Span);
    logger?.LogInformation("Received invocation for the job '{jobName}' with payload '{deserializedPayload}'",
        jobName, deserializedPayload);
    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    
    return Task.CompletedTask;
}, TimeSpan.FromSeconds(5));

using var scope = app.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

logger.LogInformation("Scheduling one-time job 'myJob' to execute 10 seconds from now");
await daprJobsClient.ScheduleJobAsync("myJob", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
    Encoding.UTF8.GetBytes("This is a test"), repeats: 10);
logger.LogInformation("Scheduled one-time job 'myJob'");

app.Run();

#pragma warning restore CS0618 // Type or member is obsolete
