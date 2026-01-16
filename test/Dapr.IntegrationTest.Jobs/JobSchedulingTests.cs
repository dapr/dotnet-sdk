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

using Dapr.Jobs;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;
using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.E2E.Test.Jobs;

public sealed class JobSchedulingTests
{
    [Fact]
    public async Task ShouldScheduleJobWithCronExpression()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"cron-job-{Guid.NewGuid():N}";

        var invocationTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
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
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> _,
                    ILogger<JobSchedulingTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received cron job {Job}", incomingJobName);
                    invocationTcs.TrySetResult(incomingJobName);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var cronSchedule = new CronExpressionBuilder()
            .Every(EveryCronPeriod.Second, 15);

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromCronExpression(cronSchedule),
            repeats: 1, overwrite: true);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(jobName, received);
    }

    [Fact]
    public async Task ShouldScheduleJobWithDateTime()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"datetime-job-{Guid.NewGuid():N}";

        var invocationTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
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
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> _,
                    ILogger<JobSchedulingTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received datetime job {Job}", incomingJobName);
                    invocationTcs.TrySetResult(incomingJobName);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var scheduledTime = DateTimeOffset.UtcNow.AddSeconds(5);
        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDateTime(scheduledTime),
            overwrite: true);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(jobName, received);
    }

    [Fact]
    public async Task ShouldScheduleJobWithStartingFrom()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"startingfrom-job-{Guid.NewGuid():N}";

        var invocationTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
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
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> _,
                    ILogger<JobSchedulingTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received job with startingFrom {Job}", incomingJobName);
                    invocationTcs.TrySetResult(incomingJobName);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var startTime = DateTimeOffset.UtcNow.AddSeconds(5);
        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(1)),
            startingFrom: startTime, repeats: 1, overwrite: true);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(jobName, received);
    }

    [Fact]
    public async Task ShouldScheduleMultipleRepeatingJob()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"repeating-job-{Guid.NewGuid():N}";

        var receivedCount = 0;
        var invocationTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
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
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> _,
                    ILogger<JobSchedulingTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received repeating job {Job} iteration {Count}", incomingJobName, receivedCount + 1);
                    var count = Interlocked.Increment(ref receivedCount);
                    if (count == 3)
                    {
                        invocationTcs.TrySetResult(count);
                    }
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(5)),
            repeats: 3, overwrite: true);

        var finalCount = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(3, finalCount);
    }
}
