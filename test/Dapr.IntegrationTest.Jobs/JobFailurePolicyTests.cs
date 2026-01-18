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
using Dapr.Jobs.Models.Responses;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.E2E.Test.Jobs;

public sealed class JobFailurePolicyTests
{
    [Fact]
    public async Task ShouldScheduleJobWithDropFailurePolicy()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"drop-policy-job-{Guid.NewGuid():N}";

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
                    ILogger<JobFailurePolicyTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received job with drop failure policy {Job}", incomingJobName);
                    invocationTcs.TrySetResult(incomingJobName);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var dropPolicy = new JobFailurePolicyDropOptions();

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            failurePolicyOptions: dropPolicy, repeats: 1, overwrite: true);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(jobName, received);

        var ex = await Assert.ThrowsAnyAsync<DaprException>(() => daprJobsClient.GetJobAsync(jobName));
        Assert.NotNull(ex.InnerException);
        Assert.Contains("job not found", ex.InnerException.Message);
    }
    
    [Fact]
    public async Task ShouldScheduleJobWithConstantFailurePolicy()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"constant-policy-job-{Guid.NewGuid():N}";

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
                    ILogger<JobFailurePolicyTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received job with constant failure policy {Job}", incomingJobName);
                    invocationTcs.TrySetResult(incomingJobName);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        const int maxRetries = 3;
        var constantPolicy = new JobFailurePolicyConstantOptions(TimeSpan.FromSeconds(5))
        {
            MaxRetries = maxRetries
        };
        
        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            failurePolicyOptions: constantPolicy, repeats: 10, overwrite: true);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(jobName, received);

        var jobDetails = await daprJobsClient.GetJobAsync(jobName);
        Assert.NotNull(jobDetails.FailurePolicy);
        Assert.Equal(JobFailurePolicy.Constant, jobDetails.FailurePolicy.Type);
        if (jobDetails.FailurePolicy is ConfiguredConstantFailurePolicy failurePolicy)
        {
            Assert.True(failurePolicy.HasMaxRetries);
            Assert.Equal(maxRetries, failurePolicy.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(5), failurePolicy.Duration);
        }
        else
        {
            Assert.Fail();
        }
    }
}
