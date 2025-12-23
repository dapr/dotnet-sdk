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
//  ------------------------------------------------------------------------

using System.Text;
using Dapr.Jobs;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;
using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.E2E.Test.Jobs;

public sealed class JobsTests
{
    [Fact]
    public async Task ShouldScheduleAndExecuteJob()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = Path.Combine(Directory.GetCurrentDirectory(), $"jobs-components-{Guid.NewGuid():N}");
        var jobName = $"e2e-job-{Guid.NewGuid():N}";

        WebApplication? app = null;
        var invocationTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // Build and initialize the harness
        var harnessBuilder = new DaprHarnessBuilder(options);
        var harness = harnessBuilder.BuildJobs(componentsDir);

        try
        {
            await harness.InitializeAsync();
            
            Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", $"http://127.0.0.1:{harness.DaprHttpPort}");
            Environment.SetEnvironmentVariable("DAPR_GRPC_ENDPOINT", $"http://127.0.0.1:{harness.DaprGrpcPort}");
            
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole();
            builder.WebHost.UseUrls($"http://0.0.0.0:{harness.AppPort}");
            // builder.Configuration.AddInMemoryCollection(new List<KeyValuePair<string, string?>>
            // {
            //     new("DAPR_HTTP_ENDPOINT", $"http://127.0.0.1:{harness.DaprHttpPort}"),
            //     new("DAPR_GRPC_ENDPOINT", $"http://127.0.0.1:{harness.DaprGrpcPort}")
            // });
            
            builder.Services.AddDaprJobsClient();
            builder.Services.AddLogging();

            app = builder.Build();

            app.MapDaprScheduledJobHandler(async (string incomingJobName, ReadOnlyMemory<byte> payload, ILogger<JobsTests>? logger, CancellationToken ct) =>
            {
                logger?.LogInformation("Received job {Job}", incomingJobName);
                invocationTcs.TrySetResult(Encoding.UTF8.GetString(payload.Span));
                await Task.CompletedTask;
            });

            await app.StartAsync();

            await using var scope = app!.Services.CreateAsyncScope();
            var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

            var payload = "Hello!"u8.ToArray();
            await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                payload, repeats: 1, overwrite: true);
            
            // Wait for the handler to confirm execution
            var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(Encoding.UTF8.GetString(payload), received);
        }
        finally
        {
            // Clean up the environment variables
            Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", null);
            Environment.SetEnvironmentVariable("DAPR_GRPC_ENDPOINT", null);
            
            await harness.DisposeAsync();
            if (app is not null)
                await app.DisposeAsync();
        }
    }
}
