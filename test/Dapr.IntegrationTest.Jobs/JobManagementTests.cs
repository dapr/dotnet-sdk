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
using Dapr.TestContainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.E2E.Test.Jobs;

public sealed class JobManagementTests
{
    [Fact]
    public async Task ShouldGetJobDetails()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"get-job-{Guid.NewGuid():N}";

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
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var payload = "Test Payload"u8.ToArray();
        var schedule = DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(10));
        var startingFrom = DateTimeOffset.UtcNow.AddMinutes(1);
        await daprJobsClient.ScheduleJobAsync(jobName, schedule, payload, startingFrom: startingFrom,
            repeats: 10, overwrite: true);
        var expected = startingFrom.ToLocalTime();

        var jobDetails = await daprJobsClient.GetJobAsync(jobName);

        Assert.NotNull(jobDetails);
        Assert.Equal(expected.ToString("O"), jobDetails.Schedule.ExpressionValue);
        Assert.Equal(10, jobDetails.RepeatCount);
        Assert.NotNull(jobDetails.Payload);
        Assert.Equal(Encoding.UTF8.GetString(payload), Encoding.UTF8.GetString(jobDetails.Payload));
    }

    [Fact]
    public async Task ShouldDeleteJob()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"delete-job-{Guid.NewGuid():N}";

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
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromHours(1)),
            overwrite: true);

        var jobDetails = await daprJobsClient.GetJobAsync(jobName);
        Assert.NotNull(jobDetails);

        await daprJobsClient.DeleteJobAsync(jobName);

        await Assert.ThrowsAsync<DaprException>(async () => 
            await daprJobsClient.GetJobAsync(jobName));
    }

    [Fact]
    public async Task ShouldOverwriteExistingJob()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"overwrite-job-{Guid.NewGuid():N}";
        
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
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var originalPayload = "Original"u8.ToArray();
        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromHours(1)),
            originalPayload, repeats: 5, overwrite: true);

        var originalDetails = await daprJobsClient.GetJobAsync(jobName);
        Assert.Equal(5, originalDetails.RepeatCount);

        var newPayload = "Updated"u8.ToArray();
        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromMinutes(30)),
            newPayload, repeats: 10, overwrite: true);

        var updatedDetails = await daprJobsClient.GetJobAsync(jobName);
        Assert.Equal(10, updatedDetails.RepeatCount);
        Assert.Equal(Encoding.UTF8.GetString(newPayload), Encoding.UTF8.GetString(updatedDetails.Payload!));
        
        await daprJobsClient.DeleteJobAsync(jobName);
    }
    
    [Fact]
    public async Task ShouldScheduleJobWithTTL()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"ttl-job-{Guid.NewGuid():N}";

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
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var startTime = DateTimeOffset.UtcNow.AddSeconds(2);
        var ttl = DateTimeOffset.UtcNow.AddMinutes(5);

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(1)),
            startingFrom: startTime, ttl: ttl, repeats: 100, overwrite: true);

        var jobDetails = await daprJobsClient.GetJobAsync(jobName);
        Assert.NotNull(jobDetails);
        Assert.NotNull(jobDetails.Ttl);
        
        await daprJobsClient.DeleteJobAsync(jobName);
    }
}
