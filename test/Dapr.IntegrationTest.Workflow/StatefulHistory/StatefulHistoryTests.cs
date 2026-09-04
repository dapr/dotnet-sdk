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

using Dapr.DurableTask.Protobuf;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.StatefulHistory;

/// <summary>
/// Wire-level verification that the sidecar really delivers history deltas.
/// </summary>
/// <remarks>
/// <para>Requires a sidecar implementing the stateful-history protocol (dapr/durabletask-go#110,
/// reaching dapr via dapr/dapr#10142). No dapr release contains it yet, so these tests only run
/// against the image the <c>integration-tests-dapr-head</c> CI job builds from dapr master, and
/// skip otherwise. Against an older sidecar the capability is ignored and every turn arrives as a
/// full send, which is exactly what <see cref="DeltaDeliveryReducesFullSends"/> is written to
/// catch.</para>
/// <para>Asserting on workflow output alone would prove nothing: a correct delta path and a sidecar
/// that never sends deltas produce identical results. The counts come from a gRPC interceptor
/// watching the real work-item stream.</para>
/// </remarks>
public sealed class StatefulHistoryTests
{
    private const int Turns = 20;

    /// <summary>
    /// The sidecar records how much history a stream holds only *after* rewriting a work item, so
    /// the first turn (empty past) leaves the watermark at zero and the second still fails the
    /// "worker holds something" check. Deltas therefore start at the third turn; dapr's largehistory
    /// integration test asserts the same bound.
    /// </summary>
    private const int MaxWarmupFullSends = 2;

    private sealed record RunResult(int Deltas, int FullSends, int HistoryFetches, int Output);

    [RequiresDaprHeadFact]
    public async Task DeltaDeliveryReducesFullSends()
    {
        var result = await RunAccumulateAsync(disableStatefulHistory: false);

        Assert.Equal(Turns, result.Output);
        Assert.True(result.Deltas > 0, $"sidecar never sent a delta: {result}");
        Assert.True(result.FullSends <= MaxWarmupFullSends, $"too many full sends: {result}");
        Assert.True(result.Deltas >= Turns - MaxWarmupFullSends,
            $"expected a delta for nearly every turn: {result}");
    }

    [RequiresDaprHeadFact]
    public async Task WarmStreamNeverMissesItsCache()
    {
        var result = await RunAccumulateAsync(disableStatefulHistory: false);

        Assert.Equal(Turns, result.Output);
        Assert.Equal(0, result.HistoryFetches);
    }

    [RequiresDaprHeadFact]
    public async Task DisabledWorkerReceivesOnlyFullHistories()
    {
        var result = await RunAccumulateAsync(disableStatefulHistory: true);

        Assert.Equal(Turns, result.Output);
        Assert.Equal(0, result.Deltas);
        Assert.True(result.FullSends >= Turns, $"expected a full send per turn: {result}");
    }

    /// <summary>
    /// Runs a long sequential activity chain, so each activity result is its own turn and the
    /// committed history grows every turn. That is what makes the omitted prefix, and therefore the
    /// delta, large enough to be worth measuring.
    /// </summary>
    private static async Task<RunResult> RunAccumulateAsync(bool disableStatefulHistory)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("stateful-history-components");
        var workflowInstanceId = Guid.NewGuid().ToString();
        var observer = new WorkItemObserver();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.DisableStatefulHistory = disableStatefulHistory;
                        opt.RegisterWorkflow<AccumulateWorkflow>();
                        opt.RegisterActivity<PlusOneActivity>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });

                // Layer the observer onto the worker's already-registered sidecar client. There is no
                // public interceptor hook, but AddGrpcClient for the same client type returns the
                // same named registration, so this only adds the interceptor.
                builder.Services
                    .AddGrpcClient<TaskHubSidecarService.TaskHubSidecarServiceClient>()
                    .AddInterceptor(() => observer);
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var workflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await workflowClient.ScheduleNewWorkflowAsync(nameof(AccumulateWorkflow), workflowInstanceId, 0);
        var state = await workflowClient.WaitForWorkflowCompletionAsync(
            workflowInstanceId, true, TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, state.RuntimeStatus);

        return new RunResult(
            observer.Deltas(workflowInstanceId),
            observer.FullSends(workflowInstanceId),
            observer.HistoryFetches(workflowInstanceId),
            state.ReadOutputAs<int>());
    }

    private sealed class PlusOneActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input + 1);
    }

    private sealed class AccumulateWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var current = input;
            for (var i = 0; i < Turns; i++)
            {
                current = await context.CallActivityAsync<int>(nameof(PlusOneActivity), current);
            }

            return current;
        }
    }
}
