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

using System.ComponentModel.DataAnnotations;
using System.Text;
using Dapr.Jobs;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.E2E.Test.Jobs;

public sealed class JobsTests
{
    [Fact]
    public async Task ShouldScheduleAndReceiveJob()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"e2e-job-{Guid.NewGuid():N}";

        var invocationTcs =
            new TaskCompletionSource<(string payload, string jobName)>(TaskCreationOptions
                .RunContinuationsAsynchronously);
        
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(options, environment).BuildJobs(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                // Explicitly configure the Dapr client with the correct endpoints
                builder.Services.AddDaprJobsClient(configure: (sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    var httpEndpoint = config["DAPR_HTTP_ENDPOINT"];

                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    if (!string.IsNullOrEmpty(httpEndpoint))
                        clientBuilder.UseHttpEndpoint(httpEndpoint);
                });
            })
            .ConfigureApp(app =>
            {
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> payload,
                    ILogger<JobsTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received job {Job}", incomingJobName);
                    invocationTcs.TrySetResult((Encoding.UTF8.GetString(payload.Span), incomingJobName));
                });
            })
            .BuildAndStartAsync();

        // Clean test logic
        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var payload = "Hello!"u8.ToArray();
        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            payload, repeats: 1, overwrite: true);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(Encoding.UTF8.GetString(payload), received.payload);
        Assert.Equal(jobName, received.jobName);
    }
}
