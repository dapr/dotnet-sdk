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
// ------------------------------------------------------------------------

using System.Globalization;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning;

public sealed class NamedVersioningWithoutAttributesIntegrationTests
{
    private const string CanonicalWorkflowName = "NoAttributeWorkflow";
    private const string ResumeEventName = "resume";

    [MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldRouteByNameWithoutWorkflowVersionAttributes()
    {
        var instanceIdV1 = Guid.NewGuid().ToString("N");
        var instanceIdV2 = Guid.NewGuid().ToString("N");
        var appId = $"workflow-versioning-noattr-{Guid.NewGuid():N}";
        var options = new DaprRuntimeOptions().WithAppId(appId);
        var componentsDirV1 = TestDirectoryManager.CreateTestDirectory("workflow-versioning-noattr-v1");
        var componentsDirV2 = TestDirectoryManager.CreateTestDirectory("workflow-versioning-noattr-v2");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        await using (var appV1 =
                     await StartVersionedAppAsync(componentsDirV1, _ => new MinVersionSelector(), environment, options))
        {
            using var scope1 = appV1.CreateScope();
            var client1 = scope1.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            var latestNameV1 = GetLatestWorkflowName(scope1.ServiceProvider);
            Assert.Equal(nameof(NoAttributeWorkflowV1), latestNameV1);

            await client1.ScheduleNewWorkflowAsync(latestNameV1, instanceIdV1, new Payload("first"));
            using (var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
            {
                try
                {
                    await client1.WaitForWorkflowStartAsync(instanceIdV1, cancellation: startCts.Token);
                }
                catch (OperationCanceledException)
                {
                    var state = await client1.GetWorkflowStateAsync(instanceIdV1, getInputsAndOutputs: false);
                    Assert.Fail($"Timed out waiting for workflow start. Current status: {state?.RuntimeStatus}.");
                }
            }
        }

        await using (var appV2 =
                     await StartVersionedAppAsync(componentsDirV2, _ => new MaxVersionSelector(), environment, options))
        {
            using var scope2 = appV2.CreateScope();
            var client2 = scope2.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            var latestNameV2 = GetLatestWorkflowName(scope2.ServiceProvider);
            Assert.Equal(nameof(NoAttributeWorkflowV2), latestNameV2);

            await client2.RaiseEventAsync(instanceIdV1, ResumeEventName, "resume");
            using var resumeCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var resumed = await client2.WaitForWorkflowCompletionAsync(instanceIdV1, cancellation: resumeCts.Token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, resumed.RuntimeStatus);
            Assert.Equal("v1:first:resume", resumed.ReadOutputAs<string>());

            await client2.ScheduleNewWorkflowAsync(latestNameV2, instanceIdV2, new Payload("second"));
            await client2.RaiseEventAsync(instanceIdV2, ResumeEventName, "resume");
            using var latestCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var latest = await client2.WaitForWorkflowCompletionAsync(instanceIdV2, cancellation: latestCts.Token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, latest.RuntimeStatus);
            Assert.Equal("v2:second:resume", latest.ReadOutputAs<string>());
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
                    configureRuntime: _ => { },
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

    private static bool IsTransientRpc(RpcException ex) =>
        ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded;

    internal sealed record Payload(string Name);

    internal sealed class NoAttributeWorkflowV1 : Workflow<Payload, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, Payload input)
        {
            var resume = await context.WaitForExternalEventAsync<string>(ResumeEventName);
            return $"v1:{input.Name}:{resume}";
        }
    }

    internal sealed class NoAttributeWorkflowV2 : Workflow<Payload, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, Payload input)
        {
            var resume = await context.WaitForExternalEventAsync<string>(ResumeEventName);
            return $"v2:{input.Name}:{resume}";
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
