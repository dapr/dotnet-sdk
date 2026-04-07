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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

/// <summary>
/// Regression tests for the bug where events arriving in the same NewEvents batch as a
/// ContinueAsNew turn were silently dropped because CarryoverEvents was snapshotted
/// mid-ProcessEvents, before the later events in the batch had been buffered.
/// </summary>
public sealed class ContinueAsNewCarryoverEventsTests
{
    private const string SignalEventName = "signal";

    /// <summary>
    /// Raises several same-name signals in parallel against a workflow that processes one
    /// signal per ContinueAsNew iteration. When the sidecar batches the concurrent signals
    /// into a single NewEvents delivery, the pre-fix code lost every signal after the first.
    /// After the fix the full buffer is captured once all events are processed, so every
    /// signal survives as a carryover event and the workflow counts down to zero.
    /// </summary>
    [Fact]
    public async Task ContinueAsNew_ShouldCarryOverEvents_WhenMultipleSignalsArriveTogether()
    {
        const int signalCount = 250;
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt => opt.RegisterWorkflow<SignalCountdownWorkflow>(),
                    configureClient: (sp, cb) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            cb.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        // Start the workflow, which will wait for `signalCount` signals one per ContinueAsNew cycle.
        await client.ScheduleNewWorkflowAsync(nameof(SignalCountdownWorkflow), workflowInstanceId,
            new SignalCountdownInput(signalCount, []));

        // Give the workflow a moment to register its first WaitForExternalEventAsync, then fire
        // all signals simultaneously. The tight timing maximises the chance that the sidecar
        // batches several of them into the same NewEvents, which is what triggers the bug.
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        await Task.WhenAll(
            Enumerable.Range(0, signalCount)
                .Select(index => client.RaiseEventAsync(workflowInstanceId, SignalEventName, index,
                    TestContext.Current.CancellationToken)));

        // All signals must be consumed via carryover before the workflow completes.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(2));

        var result = await client.WaitForWorkflowCompletionAsync(
            workflowInstanceId, cancellation: timeoutCts.Token);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);

        // Every index in [0, signalCount) must appear exactly once in the output — no drops, no duplicates.
        var receivedIndexes = result.ReadOutputAs<List<int>>();
        Assert.NotNull(receivedIndexes);
        Assert.Equal(Enumerable.Range(0, signalCount), receivedIndexes.Order());
    }

    private sealed record SignalCountdownInput(int Remaining, List<int> ReceivedIndexes);

    /// <summary>
    /// Workflow that counts down from <c>remaining</c> to zero, consuming one "signal" external
    /// event per ContinueAsNew iteration with <c>preserveUnprocessedEvents: true</c>.
    /// Each iteration appends the received event payload to <c>ReceivedIndexes</c> and carries
    /// the accumulated list forward via ContinueAsNew so the final output contains every index
    /// that was processed.
    /// </summary>
    private sealed class SignalCountdownWorkflow : Workflow<SignalCountdownInput, List<int>>
    {
        public override async Task<List<int>> RunAsync(WorkflowContext context, SignalCountdownInput input)
        {
            if (input.Remaining <= 0)
                return input.ReceivedIndexes;

            var index = await context.WaitForExternalEventAsync<int>(SignalEventName, TestContext.Current.CancellationToken);

            var updated = new List<int>(input.ReceivedIndexes) { index };
            context.ContinueAsNew(new SignalCountdownInput(input.Remaining - 1, updated), preserveUnprocessedEvents: true);
            return updated; // unreachable after ContinueAsNew but satisfies the return type
        }
    }
}
