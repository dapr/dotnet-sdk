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

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Dapr.Workflow.Registration;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

/// <summary>
/// Verifies the two distinct error surfaces of <see cref="DaprWorkflowClient.GetWorkflowStateAsync"/>.
/// </summary>
/// <remarks>
/// Modeled off a repro that showed a transient, non-<see cref="StatusCode.NotFound"/> gRPC error being
/// swallowed into an <c>Exists == false</c> <see cref="WorkflowState"/> — making it indistinguishable from a
/// workflow that genuinely does not exist. The client now only treats <see cref="StatusCode.NotFound"/> as
/// "does not exist"; every other gRPC failure propagates so the caller can handle it. This test proves both
/// surfaces end-to-end over real gRPC:
/// <list type="number">
///   <item>a never-created instance queried against a real sidecar returns <c>Exists == false</c> and does not throw;</item>
///   <item>a transport-level failure (a client pointed at an unreachable endpoint) surfaces as an <see cref="RpcException"/>.</item>
/// </list>
/// </remarks>
public sealed class GetWorkflowStateTests
{
    [Fact]
    public async Task GetWorkflowStateAsync_ShouldReturnNotExists_ForMissingWorkflow_AndPropagate_OnTransportError()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt => opt.RegisterWorkflow<NoopWorkflow>(),
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        // 1) A workflow that was never created. The runtime reports "not found", which the client maps to a
        //    concrete WorkflowState with Exists == false — no exception, exactly as the repro's baseline showed.
        var missing = await daprWorkflowClient.GetWorkflowStateAsync(
            "never-created-" + Guid.NewGuid().ToString("N"),
            cancellation: TestContext.Current.CancellationToken);

        Assert.NotNull(missing);
        Assert.False(missing.Exists);
        Assert.Equal(WorkflowRuntimeStatus.Unknown, missing.RuntimeStatus);

        // 2) A non-NotFound gRPC failure must reach the caller instead of being swallowed into the same
        //    Exists == false surface. We reproduce that deterministically with a client pointed at an
        //    unreachable endpoint: the call fails at the transport layer (StatusCode other than NotFound),
        //    and GetWorkflowStateAsync lets that RpcException propagate.
        await using var unreachableClient = new DaprWorkflowClientBuilder()
            .UseGrpcEndpoint("http://127.0.0.1:9999")
            .Build();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            unreachableClient.GetWorkflowStateAsync("any-instance", cancellation: TestContext.Current.CancellationToken));
        Assert.NotEqual(StatusCode.NotFound, ex.StatusCode);
    }

    private sealed class NoopWorkflow : Workflow<object?, object?>
    {
        public override Task<object?> RunAsync(WorkflowContext context, object? input) => Task.FromResult<object?>(null);
    }
}
