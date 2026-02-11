// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.Globalization;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning;

public sealed class VersioningIntegrationTests
{
    private const string CanonicalWorkflowName = "VersionedWorkflow";
    private const string ResumeEventName = "resume";

    [MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldResumeInFlightWorkflowWithOriginalVersionAndUseLatestForNew()
    {
        var instanceId = Guid.NewGuid().ToString("N");
        var latestInstanceId = Guid.NewGuid().ToString("N");
        var appId = $"workflow-versioning-{Guid.NewGuid():N}";
        var options = new DaprRuntimeOptions().WithAppId(appId);
        var componentsDirV1 = TestDirectoryManager.CreateTestDirectory("workflow-versioning-v1");
        var componentsDirV2 = TestDirectoryManager.CreateTestDirectory("workflow-versioning-v2");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        await using (var appV1 =
                     await StartVersionedAppAsync(componentsDirV1, _ => new MinVersionSelector(), environment, options))
        {
            using var scope1 = appV1.CreateScope();
            var client1 = scope1.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            var latestNameV1 = GetLatestWorkflowName(scope1.ServiceProvider);
            await client1.ScheduleNewWorkflowAsync(latestNameV1, instanceId, new VersionedPayload("first"));
            using (var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
            {
                try
                {
                    await client1.WaitForWorkflowStartAsync(instanceId, cancellation: startCts.Token);
                }
                catch (OperationCanceledException)
                {
                    var state = await client1.GetWorkflowStateAsync(instanceId, getInputsAndOutputs: false);
                    Assert.Fail($"Timed out waiting for workflow start. Current status: {state?.RuntimeStatus}.");
                }
            }
        }

        await using (var appV2 =
                     await StartVersionedAppAsync(componentsDirV2, _ => new MaxVersionSelector(), environment, options))
        {
            using var scope2 = appV2.CreateScope();
            var client2 = scope2.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await client2.RaiseEventAsync(instanceId, ResumeEventName, "resume");
            using var resumeCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var resumed = await client2.WaitForWorkflowCompletionAsync(instanceId, cancellation: resumeCts.Token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, resumed.RuntimeStatus);
            Assert.Equal("v1:first:resume", resumed.ReadOutputAs<string>());

            var latestNameV2 = GetLatestWorkflowName(scope2.ServiceProvider);
            await client2.ScheduleNewWorkflowAsync(latestNameV2, latestInstanceId, new VersionedPayload("second"));
            await client2.RaiseEventAsync(latestInstanceId, ResumeEventName, "resume");
            using var latestCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var latestResult =
                await client2.WaitForWorkflowCompletionAsync(latestInstanceId, cancellation: latestCts.Token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, latestResult.RuntimeStatus);
            Assert.Equal("v2:second:resume", latestResult.ReadOutputAs<string>());
        }
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldFailWorkflowWhenVersionMissing()
    {
        var instanceId = Guid.NewGuid().ToString("N");
        var appId = $"workflow-versioning-stalled-{Guid.NewGuid():N}";
        var options = new DaprRuntimeOptions().WithAppId(appId);
        var componentsDirV1 = TestDirectoryManager.CreateTestDirectory("workflow-versioning-stall-v1");
        var componentsDirV2 = TestDirectoryManager.CreateTestDirectory("workflow-versioning-stall-v2");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        await using (var appV1 =
                     await StartVersionedAppAsync(componentsDirV1, _ => new MinVersionSelector(), environment, options))
        {
            using var scope1 = appV1.CreateScope();
            var client1 = scope1.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            var latestNameV1 = GetLatestWorkflowName(scope1.ServiceProvider);
            await client1.ScheduleNewWorkflowAsync(latestNameV1, instanceId, new VersionedPayload("stalled"));
            using (var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
            {
                try
                {
                    await client1.WaitForWorkflowStartAsync(instanceId, cancellation: startCts.Token);
                }
                catch (OperationCanceledException)
                {
                    var state = await client1.GetWorkflowStateAsync(instanceId, getInputsAndOutputs: false);
                    Assert.Fail($"Timed out waiting for workflow start. Current status: {state?.RuntimeStatus}.");
                }
            }

            var startedState = await client1.GetWorkflowStateAsync(instanceId, getInputsAndOutputs: false);
            Assert.True(startedState?.Exists, "Expected workflow instance to exist before shutdown.");
        }

        await using (var appV2 = await StartNonVersionedAppAsync(componentsDirV2, environment, options))
        {
            using var scope2 = appV2.CreateScope();
            var client2 = scope2.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await WaitForWorkflowExistsAsync(client2, instanceId, TimeSpan.FromMinutes(1));
            await RaiseEventWithRetryAsync(client2, instanceId, ResumeEventName, "resume", TimeSpan.FromMinutes(1));
            var stalledState = await WaitForStatusAsync(client2, instanceId, TimeSpan.FromMinutes(1), WorkflowRuntimeStatus.Failed);
            Assert.Equal(WorkflowRuntimeStatus.Failed, stalledState.RuntimeStatus);
        }
    }

    private static async Task<DaprTestApplication> StartVersionedAppAsync(
        string componentsDir,
        Func<IServiceProvider, IWorkflowVersionSelector> selectorFactory,
        DaprTestEnvironment? environment = null,
        DaprRuntimeOptions? options = null)
    {
        var harnessBuilder = new DaprHarnessBuilder(componentsDir);

        if (environment is not null)
        {
            harnessBuilder.WithEnvironment(environment);
        }

        if (options is not null)
        {
            harnessBuilder.WithOptions(options);
        }

        var harness = harnessBuilder.BuildWorkflow();        
        var app = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowVersioning(versioning =>
                {
                    versioning.DefaultStrategy = _ => new NumericVersionStrategy();
                    versioning.DefaultSelector = selectorFactory;
                });

                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterActivity<NoopActivity>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();
        await WaitForSidecarAsync(app, TimeSpan.FromMinutes(1));
        return app;
    }

    private static async Task<DaprTestApplication> StartNonVersionedAppAsync(
        string componentsDir,
        DaprTestEnvironment? environment = null,
        DaprRuntimeOptions? options = null)
    {
        var harnessBuilder = new DaprHarnessBuilder(componentsDir);

        if (environment is not null)
            harnessBuilder.WithEnvironment(environment);

        if (options is not null)
            harnessBuilder.WithOptions(options);

        var harness = harnessBuilder.BuildWorkflow();
        var app = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<VersionedWorkflowV2>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();
        await WaitForSidecarAsync(app, TimeSpan.FromMinutes(1));
        return app;
    }

    private static string GetLatestWorkflowName(IServiceProvider services)
    {
        var registry = GeneratedWorkflowVersionRegistry.GetWorkflowVersionRegistry(services);
        if (!registry.TryGetValue(CanonicalWorkflowName, out var versions) || versions.Count == 0)
        {
            throw new InvalidOperationException($"No workflow versions found for '{CanonicalWorkflowName}'.");
        }

        return NormalizeWorkflowTypeName(versions[0]);
    }

    private static string NormalizeWorkflowTypeName(string typeName)
    {
        var trimmed = typeName;
        if (trimmed.StartsWith("global::", StringComparison.Ordinal))
        {
            trimmed = trimmed["global::".Length..];
        }

        var lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 ? trimmed[(lastDot + 1)..] : trimmed;
    }

    private static async Task<WorkflowState> WaitForStatusAsync(
        DaprWorkflowClient client,
        string instanceId,
        TimeSpan timeout,
        params WorkflowRuntimeStatus[] statuses)
    {
        var stopAt = DateTime.UtcNow + timeout;
        WorkflowState? state = null;

        while (DateTime.UtcNow < stopAt)
        {
            try
            {
                state = await client.GetWorkflowStateAsync(instanceId, getInputsAndOutputs: false);
            }
            catch (RpcException ex) when (IsTransientRpc(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                continue;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                continue;
            }

            if (state is not null && statuses.Contains(state.RuntimeStatus))
            {
                return state;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        var lastStatus = state?.RuntimeStatus.ToString() ?? "unknown";
        var exists = state?.Exists ?? false;
        Assert.Fail($"Timed out waiting for workflow status {statuses}. Last status: {lastStatus}. Exists: {exists}.");
        return state!;
    }

    private static async Task RaiseEventWithRetryAsync(
        DaprWorkflowClient client,
        string instanceId,
        string eventName,
        object? eventPayload,
        TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            try
            {
                await client.RaiseEventAsync(instanceId, eventName, eventPayload);
                return;
            }
            catch (RpcException ex) when (IsTransientRpc(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        Assert.Fail($"Timed out raising event '{eventName}' for workflow instance '{instanceId}'.");
    }

    private static async Task ScheduleNewWorkflowWithRetryAsync(
        DaprWorkflowClient client,
        string workflowName,
        string instanceId,
        object? input,
        TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            try
            {
                await client.ScheduleNewWorkflowAsync(workflowName, instanceId, input);
                return;
            }
            catch (RpcException ex) when (IsTransientRpc(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        Assert.Fail($"Timed out scheduling workflow '{workflowName}' for instance '{instanceId}'.");
    }

    private static async Task WaitForSidecarAsync(DaprTestApplication app, TimeSpan timeout)
    {
        using var scope = app.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        var stopAt = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < stopAt)
        {
            try
            {
                await client.GetWorkflowStateAsync($"warmup-{Guid.NewGuid():N}", getInputsAndOutputs: false);
                return;
            }
            catch (RpcException ex) when (IsTransientRpc(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        Assert.Fail("Timed out waiting for Dapr sidecar readiness.");
    }

    private static async Task WaitForWorkflowExistsAsync(
        DaprWorkflowClient client,
        string instanceId,
        TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            try
            {
                var state = await client.GetWorkflowStateAsync(instanceId, getInputsAndOutputs: false);
                if (state?.Exists == true)
                {
                    return;
                }
            }
            catch (RpcException ex) when (IsTransientRpc(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                continue;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                continue;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        Assert.Fail($"Timed out waiting for workflow instance '{instanceId}' to exist.");
    }

    private static bool IsTransientRpc(RpcException ex) =>
        ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded;

    internal sealed record VersionedPayload(string Name);

    [WorkflowVersion(CanonicalName = CanonicalWorkflowName, Version = "1")]
    internal sealed class VersionedWorkflowV1 : Workflow<VersionedPayload, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, VersionedPayload input)
        {
            await context.CallActivityAsync<string>(nameof(NoopActivity), "noop");
            var eventValue = await context.WaitForExternalEventAsync<string>(ResumeEventName);
            return $"v1:{input.Name}:{eventValue}";
        }
    }

    [WorkflowVersion(CanonicalName = CanonicalWorkflowName, Version = "2")]
    internal sealed class VersionedWorkflowV2 : Workflow<VersionedPayload, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, VersionedPayload input)
        {
            await context.CallActivityAsync<string>(nameof(NoopActivity), "noop");
            var eventValue = await context.WaitForExternalEventAsync<string>(ResumeEventName);
            return $"v2:{input.Name}:{eventValue}";
        }
    }

    private sealed class NoopActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        {
            return Task.FromResult(input);
        }
    }

    private sealed class NumericVersionStrategy : IWorkflowVersionStrategy
    {
        public bool TryParse(string typeName, out string canonicalName, out string version)
        {
            canonicalName = string.Empty;
            version = string.Empty;

            if (string.IsNullOrWhiteSpace(typeName))
                return false;

            var trimmed = typeName.Trim();
            var index = trimmed.LastIndexOf('V');
            if (index <= 0 || index == trimmed.Length - 1)
                return false;

            var candidateVersion = trimmed[(index + 1)..];
            if (!int.TryParse(candidateVersion, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                return false;

            canonicalName = trimmed[..index];
            version = candidateVersion;
            return true;
        }

        public int Compare(string? v1, string? v2)
        {
            var left = int.Parse(v1 ?? "0", CultureInfo.InvariantCulture);
            var right = int.Parse(v2 ?? "0", CultureInfo.InvariantCulture);
            return left.CompareTo(right);
        }
    }

    private sealed class MinVersionSelector : IWorkflowVersionSelector
    {
        public WorkflowVersionIdentity SelectLatest(
            string canonicalName,
            IReadOnlyCollection<WorkflowVersionIdentity> candidates,
            IWorkflowVersionStrategy strategy)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(strategy);
            ArgumentOutOfRangeException.ThrowIfEqual(0, candidates.Count, nameof(candidates));

            return candidates.OrderBy(v => v.Version, strategy).First();
        }
    }
}
