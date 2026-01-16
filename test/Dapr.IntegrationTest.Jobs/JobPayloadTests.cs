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
using System.Text.Json;
using Dapr.Jobs;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;
using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace Dapr.E2E.Test.Jobs;

public sealed class JobPayloadTests
{
    [Fact]
    public async Task ShouldHandleEmptyPayload()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"empty-payload-job-{Guid.NewGuid():N}";

        var invocationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> payload,
                    ILogger<JobPayloadTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received job with empty payload {Job}", incomingJobName);
                    invocationTcs.TrySetResult(payload.IsEmpty);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            payload: null, repeats: 1, overwrite: true);

        var isEmpty = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(isEmpty);
    }

    [Fact]
    public async Task ShouldHandleJsonPayload()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"json-payload-job-{Guid.NewGuid():N}";

        var invocationTcs = new TaskCompletionSource<TestPayload>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> payload,
                    ILogger<JobPayloadTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received job with JSON payload {Job}", incomingJobName);
                    var payloadStr = Encoding.UTF8.GetString(payload.Span);
                    var deserializedPayload = JsonSerializer.Deserialize<TestPayload>(payloadStr);
                    invocationTcs.TrySetResult(deserializedPayload!);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var testPayload = new TestPayload("Test Message", 42, DateTimeOffset.UtcNow);
        var jsonPayload = JsonSerializer.SerializeToUtf8Bytes(testPayload);

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            jsonPayload, repeats: 1, overwrite: true);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(testPayload.Message, received.Message);
        Assert.Equal(testPayload.Value, received.Value);
    }

    // [Fact]
    // public async Task ShouldHandleLargePayload()
    // {
    //     var options = new DaprRuntimeOptions();
    //     var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
    //     var jobName = $"large-payload-job-{Guid.NewGuid():N}";
    //
    //     var invocationTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    //     
    //     await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
    //     await environment.StartAsync();
    //
    //     var harness = new DaprHarnessBuilder(options, environment).BuildJobs(componentsDir);
    //     await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
    //         .ConfigureServices(builder =>
    //         {
    //             builder.Services.AddDaprJobsClient(configure: (sp, clientBuilder) =>
    //             {
    //                 var config = sp.GetRequiredService<IConfiguration>();
    //                 var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
    //                 var httpEndpoint = config["DAPR_HTTP_ENDPOINT"];
    //
    //                 if (!string.IsNullOrEmpty(grpcEndpoint))
    //                     clientBuilder.UseGrpcEndpoint(grpcEndpoint);
    //                 if (!string.IsNullOrEmpty(httpEndpoint))
    //                     clientBuilder.UseHttpEndpoint(httpEndpoint);
    //             });
    //         })
    //         .ConfigureApp(app =>
    //         {
    //             app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> payload,
    //                 ILogger<JobPayloadTests>? logger, CancellationToken _) =>
    //             {
    //                 logger?.LogInformation("Received job with large payload {Job}", incomingJobName);
    //                 invocationTcs.TrySetResult(payload.Length);
    //             });
    //         })
    //         .BuildAndStartAsync();
    //
    //     using var scope = testApp.CreateScope();
    //     var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();
    //
    //     var largePayload = new byte[10000];
    //     new Random().NextBytes(largePayload);
    //     
    //     // Give the HTTP pipeline a moment to fully initialize in .NET 10
    //     await Task.Delay(TimeSpan.FromSeconds(1));
    //
    //     await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
    //         largePayload, repeats: 1, overwrite: true);
    //
    //     var receivedSize = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    //     Assert.Equal(largePayload.Length, receivedSize);
    // }

    [Fact]
    public async Task ShouldHandleBinaryPayload()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"binary-payload-job-{Guid.NewGuid():N}";

        var invocationTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        
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
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> payload,
                    ILogger<JobPayloadTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received job with binary payload {Job}", incomingJobName);
                    invocationTcs.TrySetResult(payload.ToArray());
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var binaryPayload = new byte[] { 0x00, 0xFF, 0x42, 0xAB, 0xCD, 0xEF };

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            binaryPayload, repeats: 1, overwrite: true);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(binaryPayload, received);
    }

    private record TestPayload(string Message, int Value, DateTimeOffset Timestamp);
}
