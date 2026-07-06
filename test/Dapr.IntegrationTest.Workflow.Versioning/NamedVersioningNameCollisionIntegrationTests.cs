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

namespace Dapr.IntegrationTest.Workflow.Versioning
{
    internal static class CollisionWorkflowNames
    {
        public const string CanonicalWorkflowName = "CollisionWorkflowFamily";
        public const string SimpleWorkflowName = "CollisionWorkflow";
    }

    public sealed class NamedVersioningNameCollisionIntegrationTests
    {
        [MinimumDaprRuntimeFact("1.17")]
        public async Task ShouldDeterministicallyResolveCollidingWorkflowNames()
        {
            var instanceId = Guid.NewGuid().ToString("N");
            var appId = $"workflow-versioning-collision-{Guid.NewGuid():N}";
            var options = new DaprRuntimeOptions().WithAppId(appId);
            var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-versioning-collision");

            await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
            await environment.StartAsync();

            await using var app = await StartVersionedAppAsync(componentsDir, environment, options);
            using var scope = app.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            var registry = GeneratedWorkflowVersionRegistry.GetWorkflowVersionRegistry(scope.ServiceProvider);
            Assert.True(registry.TryGetValue(CollisionWorkflowNames.CanonicalWorkflowName, out var versions));
            Assert.Equal(2, versions.Count);

            var normalized = versions.Select(NormalizeWorkflowTypeName).ToArray();
            Assert.All(normalized, name => Assert.Equal(CollisionWorkflowNames.SimpleWorkflowName, name));

            await client.ScheduleNewWorkflowAsync(CollisionWorkflowNames.SimpleWorkflowName, instanceId, 1);
            using var completionCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: completionCts.Token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal(GetExpectedCollisionWinner(), result.ReadOutputAs<string>());
        }

        private static async Task<DaprTestApplication> StartVersionedAppAsync(
            string componentsDir,
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
                        versioning.DefaultSelector = _ => new MaxVersionSelector();
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

        private static string GetExpectedCollisionWinner()
        {
            var alphaName = $"global::{typeof(CollisionAlpha.CollisionWorkflow).FullName}";
            var betaName = $"global::{typeof(CollisionBeta.CollisionWorkflow).FullName}";
            return string.CompareOrdinal(alphaName, betaName) <= 0 ? "alpha" : "beta";
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
    }
}

namespace Dapr.IntegrationTest.Workflow.Versioning.CollisionAlpha
{
    using Dapr.Workflow;
    using Dapr.Workflow.Versioning;

    [WorkflowVersion(CanonicalName = CollisionWorkflowNames.CanonicalWorkflowName, Version = "1")]
    internal sealed class CollisionWorkflow : Workflow<int, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, int input)
        {
            return Task.FromResult("alpha");
        }
    }
}

namespace Dapr.IntegrationTest.Workflow.Versioning.CollisionBeta
{
    using Dapr.Workflow;
    using Dapr.Workflow.Versioning;

    [WorkflowVersion(CanonicalName = CollisionWorkflowNames.CanonicalWorkflowName, Version = "2")]
    internal sealed class CollisionWorkflow : Workflow<int, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, int input)
        {
            return Task.FromResult("beta");
        }
    }
}
