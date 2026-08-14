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
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.IntegrationTest.Jobs;

/// <summary>
/// Integration tests that validate the Dapr Jobs SDK correctly receives job trigger
/// callbacks via both the gRPC <c>AppCallbackAlpha.OnJobEventAlpha1</c> path and the HTTP
/// POST endpoint. <c>MapDaprScheduledJobHandler</c> registers both handlers simultaneously;
/// the Dapr sidecar invokes whichever protocol it is configured for (via its
/// <c>--app-protocol</c> flag) and the other remains idle.
/// </summary>
/// <remarks>
/// gRPC over plaintext requires Kestrel to be configured for <see cref="HttpProtocols.Http2"/>
/// because HTTP/2 needs either TLS+ALPN or explicit HTTP/2-only endpoint configuration.
/// The gRPC test helper configures this via <c>ConfigureKestrel</c>; the HTTP tests use the
/// default Kestrel configuration (HTTP/1).
/// </remarks>
public sealed class JobGrpcCallbackTests
{
    /// <summary>
    /// Configures services for gRPC job callbacks, including the required Kestrel HTTP/2
    /// configuration for plaintext gRPC (h2c).
    /// </summary>
    private static void ConfigureGrpcJobServices(WebApplicationBuilder builder)
    {
        // HTTP/2 is required for gRPC over plaintext (h2c). Http1AndHttp2 does NOT work
        // because HTTP/2 negotiation requires TLS+ALPN. With Http2-only, the sidecar's
        // gRPC calls connect via HTTP/2 prior knowledge.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listen => listen.Protocols = HttpProtocols.Http2);
        });

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
    }

    /// <summary>
    /// Configures services for HTTP job callbacks (the default). No Kestrel configuration
    /// is needed — the default HTTP/1 protocol works for the HTTP callback endpoint.
    /// </summary>
    private static void ConfigureHttpJobServices(WebApplicationBuilder builder) =>
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

    /// <summary>
    /// Creates a harness builder configured for gRPC app callbacks.
    /// </summary>
    private static DaprHarnessBuilder CreateGrpcHarnessBuilder(string componentsDir) =>
        new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions("1.17.0-rc.3").WithAppProtocol("grpc"));

    /// <summary>
    /// Creates a harness builder configured for the default HTTP app channel.
    /// </summary>
    private static DaprHarnessBuilder CreateHttpHarnessBuilder(string componentsDir) =>
        new DaprHarnessBuilder(componentsDir);

    [MinimumDaprRuntimeFact("1.16")]
    public async Task ShouldScheduleAndReceiveJobViaGrpcCallback()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-grpc-component");
        var jobName = $"grpc-job-{Guid.NewGuid():N}";

        var invocationTcs =
            new TaskCompletionSource<(string payload, string jobName)>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment =
            await DaprTestEnvironment.CreateWithPooledNetworkAsync(
                cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = CreateGrpcHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(ConfigureGrpcJobServices)
            .ConfigureApp(app =>
            {
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> payload,
                    ILogger<JobGrpcCallbackTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received gRPC job {Job}", incomingJobName);
                    invocationTcs.TrySetResult((Encoding.UTF8.GetString(payload.Span), incomingJobName));
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var payload = "Hello from gRPC!"u8.ToArray();
        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            payload, repeats: 1, overwrite: true,
            cancellationToken: TestContext.Current.CancellationToken);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetString(payload), received.payload);
        Assert.Equal(jobName, received.jobName);
    }

    [MinimumDaprRuntimeFact("1.16")]
    public async Task ShouldHandleEmptyPayloadViaGrpcCallback()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-grpc-component");
        var jobName = $"grpc-empty-{Guid.NewGuid():N}";

        var invocationTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment =
            await DaprTestEnvironment.CreateWithPooledNetworkAsync(
                cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = CreateGrpcHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(ConfigureGrpcJobServices)
            .ConfigureApp(app =>
            {
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> payload,
                    ILogger<JobGrpcCallbackTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received gRPC job with empty payload {Job}", incomingJobName);
                    invocationTcs.TrySetResult(payload.IsEmpty);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            payload: null, repeats: 1, overwrite: true,
            cancellationToken: TestContext.Current.CancellationToken);

        var isEmpty = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);
        Assert.True(isEmpty);
    }

    [MinimumDaprRuntimeFact("1.16")]
    public async Task ShouldHandleJsonPayloadViaGrpcCallback()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-grpc-component");
        var jobName = $"grpc-json-{Guid.NewGuid():N}";

        var invocationTcs =
            new TaskCompletionSource<TestPayload>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment =
            await DaprTestEnvironment.CreateWithPooledNetworkAsync(
                cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = CreateGrpcHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(ConfigureGrpcJobServices)
            .ConfigureApp(app =>
            {
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> payload,
                    ILogger<JobGrpcCallbackTests>? logger, CancellationToken _) =>
                {
                    logger?.LogInformation("Received gRPC job with JSON payload {Job}", incomingJobName);
                    var payloadStr = Encoding.UTF8.GetString(payload.Span);
                    var deserialized = JsonSerializer.Deserialize<TestPayload>(payloadStr);
                    invocationTcs.TrySetResult(deserialized!);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        var testPayload = new TestPayload("gRPC Message", 42, DateTimeOffset.UtcNow);
        var jsonPayload = JsonSerializer.SerializeToUtf8Bytes(testPayload);

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            jsonPayload, repeats: 1, overwrite: true,
            cancellationToken: TestContext.Current.CancellationToken);

        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);
        Assert.Equal(testPayload.Message, received.Message);
        Assert.Equal(testPayload.Value, received.Value);
    }

    [MinimumDaprRuntimeFact("1.16")]
    public async Task ShouldScheduleMultipleRepeatingJobViaGrpcCallback()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-grpc-component");
        var jobName = $"grpc-repeat-{Guid.NewGuid():N}";

        var receivedCount = 0;
        var invocationTcs =
            new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment =
            await DaprTestEnvironment.CreateWithPooledNetworkAsync(
                cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = CreateGrpcHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(ConfigureGrpcJobServices)
            .ConfigureApp(app =>
            {
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> _,
                    ILogger<JobGrpcCallbackTests>? logger, CancellationToken _) =>
                {
                    var count = Interlocked.Increment(ref receivedCount);
                    logger?.LogInformation(
                        "Received gRPC repeating job {Job} iteration {Count}", incomingJobName, count);
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
            repeats: 3, overwrite: true,
            cancellationToken: TestContext.Current.CancellationToken);

        var finalCount = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken);
        Assert.Equal(3, finalCount);
    }

    /// <summary>
    /// When the sidecar is configured for gRPC callbacks, the handler must be invoked
    /// exactly once — only via gRPC, never also via HTTP. Both handlers are registered,
    /// but the sidecar only uses the gRPC channel.
    /// </summary>
    [MinimumDaprRuntimeFact("1.16")]
    public async Task GrpcCallback_HandlerCalledExactlyOnce_NoHttpDuplicate()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-grpc-component");
        var jobName = $"grpc-exclusive-{Guid.NewGuid():N}";

        var handlerCallCount = 0;
        var invocationTcs =
            new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment =
            await DaprTestEnvironment.CreateWithPooledNetworkAsync(
                cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = CreateGrpcHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(ConfigureGrpcJobServices)
            .ConfigureApp(app =>
            {
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> _,
                    ILogger<JobGrpcCallbackTests>? logger, CancellationToken _) =>
                {
                    var count = Interlocked.Increment(ref handlerCallCount);
                    logger?.LogInformation(
                        "gRPC handler invoked {Count} time(s) for job {Job}", count, incomingJobName);
                    invocationTcs.TrySetResult(incomingJobName);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            repeats: 1, overwrite: true, cancellationToken: TestContext.Current.CancellationToken);

        // Wait for the gRPC callback to fire.
        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);
        Assert.Equal(jobName, received);

        // Allow a brief window for any potential duplicate HTTP invocation that should never arrive.
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // The handler must have been called exactly once — only via gRPC, never via HTTP.
        Assert.Equal(1, handlerCallCount);
    }

    /// <summary>
    /// When the sidecar is configured for HTTP callbacks (the default), the handler must
    /// be invoked exactly once — only via HTTP, never also via gRPC. Both handlers are
    /// registered, but the sidecar only uses the HTTP channel.
    /// </summary>
    [MinimumDaprRuntimeFact("1.16")]
    public async Task HttpCallback_HandlerCalledExactlyOnce_NoGrpcDuplicate()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("jobs-component");
        var jobName = $"http-exclusive-{Guid.NewGuid():N}";

        var handlerCallCount = 0;
        var invocationTcs =
            new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var environment =
            await DaprTestEnvironment.CreateWithPooledNetworkAsync(
                cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = CreateHttpHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildJobs();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(ConfigureHttpJobServices)
            .ConfigureApp(app =>
            {
                app.MapDaprScheduledJobHandler((string incomingJobName, ReadOnlyMemory<byte> _,
                    ILogger<JobGrpcCallbackTests>? logger, CancellationToken _) =>
                {
                    var count = Interlocked.Increment(ref handlerCallCount);
                    logger?.LogInformation(
                        "HTTP handler invoked {Count} time(s) for job {Job}", count, incomingJobName);
                    invocationTcs.TrySetResult(incomingJobName);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

        await daprJobsClient.ScheduleJobAsync(jobName, DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
            repeats: 1, overwrite: true, cancellationToken: TestContext.Current.CancellationToken);

        // Wait for the HTTP callback to fire.
        var received = await invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);
        Assert.Equal(jobName, received);

        // Allow a brief window for any potential duplicate gRPC invocation that should never arrive.
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // The handler must have been called exactly once — only via HTTP, never via gRPC.
        Assert.Equal(1, handlerCallCount);
    }

    private record TestPayload(string Message, int Value, DateTimeOffset Timestamp);
}
