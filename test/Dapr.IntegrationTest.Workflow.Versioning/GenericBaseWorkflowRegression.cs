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

using Dapr.IntegrationTest.Workflow.Versioning.ReferenceWorkflows;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning;

// Regression coverage for https://github.com/dapr/dotnet-sdk/issues/1859
//
// An abstract open-generic workflow base class must NOT be emitted into the
// generated versioning registry (it would produce CS0246 references to the
// unbound type parameters). Only the concrete closed derivative is registered.

internal static class GenericBaseWorkflowConstants
{
    public const string CanonicalName = "GenericBaseWorkflow";
}

internal abstract class VersionedWorkflowBase<TInput, TOutput> : Workflow<TInput, TOutput>
{
}

[WorkflowVersion(CanonicalName = GenericBaseWorkflowConstants.CanonicalName, Version = "1")]
internal sealed partial class ConcreteGenericBaseWorkflow : VersionedWorkflowBase<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input)
        => Task.FromResult($"generic-base-v1:{input}");
}

[WorkflowVersion(CanonicalName = GenericBaseWorkflowConstants.CanonicalName, Version = "2")]
internal sealed partial class ConcreteGenericBaseWorkflowV2 : VersionedWorkflowBase<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input)
        => Task.FromResult($"generic-base-v2:{input}");
}

public sealed class GenericBaseWorkflowRegressionTests
{
    /// <summary>
    /// Verifies that the generated registry includes the concrete closed derivatives
    /// (both versions) but excludes the abstract open-generic base class, and that
    /// version selection correctly picks V2 as the latest.
    /// </summary>
    [Fact]
    public void GeneratedRegistryShouldExcludeAbstractGenericBaseAndSelectLatestVersion()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprWorkflowVersioning();
        services.AddDaprWorkflowBuilder(configureRuntime: _ => { });

        using var provider = services.BuildServiceProvider();

        // ── Registry should contain both concrete versions under the same canonical name ──
        var registry = GeneratedWorkflowVersionRegistry.GetWorkflowVersionRegistry(provider);
        Assert.True(registry.TryGetValue(GenericBaseWorkflowConstants.CanonicalName, out var versions));
        Assert.NotNull(versions);
        Assert.Equal(2, versions!.Count);
        Assert.Contains(versions, v => v.EndsWith("ConcreteGenericBaseWorkflow", StringComparison.Ordinal));
        Assert.Contains(versions, v => v.EndsWith("ConcreteGenericBaseWorkflowV2", StringComparison.Ordinal));

        // ── V2 should be first (selected as latest by MaxVersionSelector) ──
        Assert.True(versions[0].EndsWith("ConcreteGenericBaseWorkflowV2", StringComparison.Ordinal),
            $"Expected V2 to be selected as latest, but first entry was '{versions[0]}'.");

        // ── Registry should NOT contain any entry referencing the open-generic base ──
        Assert.DoesNotContain(registry.Keys, k => k.Contains("VersionedWorkflowBase", StringComparison.Ordinal));
        Assert.DoesNotContain(
            registry.SelectMany(kv => kv.Value),
            v => v.Contains("VersionedWorkflowBase", StringComparison.Ordinal));

        // ── Cross-assembly generic base should also be excluded, concrete included ──
        Assert.True(registry.TryGetValue(
            CrossAssemblyGenericBaseWorkflowConstants.CanonicalName, out var crossAppVersions));
        Assert.Contains(crossAppVersions!,
            v => v.EndsWith("ConcreteCrossAppGenericWorkflow", StringComparison.Ordinal));
        Assert.DoesNotContain(crossAppVersions!,
            v => v.Contains("GenericBaseWorkflow<", StringComparison.Ordinal));

        // ── Factory should resolve the latest version (V2) by canonical name ──
        var factory = provider.GetRequiredService<IWorkflowsFactory>();
        Assert.True(factory.TryCreateWorkflow(
            new TaskIdentifier(GenericBaseWorkflowConstants.CanonicalName),
            provider, out var workflow, out var activationException));
        Assert.Null(activationException);
        Assert.NotNull(workflow);
        Assert.IsType<ConcreteGenericBaseWorkflowV2>(workflow);
    }

    /// <summary>
    /// Verifies that both versions inheriting through a generic base class are
    /// individually addressable by their concrete type name, and that the
    /// <see cref="IWorkflowVersionResolver"/> correctly selects V2 as latest.
    /// </summary>
    [Fact]
    public void BothVersionsInheritingThroughGenericBaseShouldBeIndividuallyAddressable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprWorkflowVersioning();
        services.AddDaprWorkflowBuilder(configureRuntime: _ => { });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IWorkflowsFactory>();

        // ── Canonical name should resolve to V2 (latest) ──
        Assert.True(factory.TryCreateWorkflow(
            new TaskIdentifier(GenericBaseWorkflowConstants.CanonicalName),
            provider, out var canonicalWorkflow, out var canonicalException));
        Assert.Null(canonicalException);
        Assert.IsType<ConcreteGenericBaseWorkflowV2>(canonicalWorkflow);

        // ── V1 concrete type name should resolve to V1 ──
        Assert.True(factory.TryCreateWorkflow(
            new TaskIdentifier(nameof(ConcreteGenericBaseWorkflow)),
            provider, out var v1Workflow, out var v1Exception));
        Assert.Null(v1Exception);
        Assert.IsType<ConcreteGenericBaseWorkflow>(v1Workflow);

        // ── V2 concrete type name should resolve to V2 ──
        Assert.True(factory.TryCreateWorkflow(
            new TaskIdentifier(nameof(ConcreteGenericBaseWorkflowV2)),
            provider, out var v2Workflow, out var v2Exception));
        Assert.Null(v2Exception);
        Assert.IsType<ConcreteGenericBaseWorkflowV2>(v2Workflow);

        // ── Version resolver should select V2 as latest for the family ──
        var resolver = provider.GetRequiredService<IWorkflowVersionResolver>();
        var family = new WorkflowFamily(
            GenericBaseWorkflowConstants.CanonicalName,
            new[]
            {
                new WorkflowVersionIdentity(GenericBaseWorkflowConstants.CanonicalName, "1",
                    nameof(ConcreteGenericBaseWorkflow)),
                new WorkflowVersionIdentity(GenericBaseWorkflowConstants.CanonicalName, "2",
                    nameof(ConcreteGenericBaseWorkflowV2)),
            });

        Assert.True(resolver.TryGetLatest(family, out var latest, out var diagId, out var diagMessage));
        Assert.Null(diagId);
        Assert.Null(diagMessage);
        Assert.Equal("2", latest.Version);
        Assert.Equal(nameof(ConcreteGenericBaseWorkflowV2), latest.TypeName);
    }

    /// <summary>
    /// Executes the workflow that inherits through an abstract generic base class
    /// to verify it runs correctly at runtime through the Dapr sidecar. Scheduling
    /// by canonical name should route to V2 (the latest version).
    /// </summary>
    [MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldExecuteWorkflowInheritingThroughGenericBase()
    {
        var instanceId = Guid.NewGuid().ToString("N");
        var appId = $"workflow-generic-base-{Guid.NewGuid():N}";
        var options = new DaprRuntimeOptions().WithAppId(appId);
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-generic-base");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        await using var app = await StartVersionedAppAsync(componentsDir, environment, options);
        using var scope = app.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(
            GenericBaseWorkflowConstants.CanonicalName, instanceId, "hello");

        using var completionCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var state = await client.WaitForWorkflowCompletionAsync(
            instanceId, cancellation: completionCts.Token);

        Assert.Equal(WorkflowRuntimeStatus.Completed, state.RuntimeStatus);
        Assert.Equal("generic-base-v2:hello", state.ReadOutputAs<string>());
    }

    private static async Task<DaprTestApplication> StartVersionedAppAsync(
        string componentsDir,
        DaprTestEnvironment environment,
        DaprRuntimeOptions options)
    {
        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .WithOptions(options)
            .BuildWorkflow();

        var app = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: _ => { },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                        {
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                        }
                    });

                builder.Services.AddDaprWorkflowVersioning();
            })
            .BuildAndStartAsync();

        await WaitForSidecarAsync(app, TimeSpan.FromMinutes(1));
        return app;
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
}
