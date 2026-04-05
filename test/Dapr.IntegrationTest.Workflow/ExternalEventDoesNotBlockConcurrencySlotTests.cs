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
/// Verifies that workflows suspended on <see cref="WorkflowContext.WaitForExternalEventAsync{T}"/>
/// do not occupy a concurrency slot, allowing additional workflows to run while the first
/// batch is waiting.
/// </summary>
public sealed class ExternalEventDoesNotBlockConcurrencySlotTests
{
    private const string WaitingStatus = "WaitingForEvent";
    private const string EventName = "ContinueSignal";

    /// <summary>
    /// With <see cref="WorkflowRuntimeOptions.MaxConcurrentWorkflows"/> set to 3, schedule
    /// 3 workflows that each wait on an external event, then schedule a 4th workflow and
    /// confirm it completes before releasing the waiting ones.
    /// </summary>
    [Fact]
    public async Task FourthWorkflow_ShouldComplete_WhileFirstThreeAreWaitingOnExternalEvent()
    {
        const int concurrencyLimit = 3;

        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");

        // Three workflows that will block on an external event.
        var waitingIds = Enumerable.Range(0, concurrencyLimit)
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();

        // One workflow that should run immediately even though the limit is 3.
        var fourthId = Guid.NewGuid().ToString();

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
                    configureRuntime: opt =>
                    {
                        opt.MaxConcurrentWorkflows = concurrencyLimit;
                        opt.RegisterWorkflow<WaitForEventWorkflow>();
                        opt.RegisterWorkflow<EchoWorkflow>();
                        opt.RegisterActivity<EchoActivity>();
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

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        // Schedule all three waiting workflows and let them reach their suspended state.
        await Task.WhenAll(waitingIds.Select(id =>
            daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(WaitForEventWorkflow), id, id)));

        using var waitCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await Task.WhenAll(waitingIds.Select(id =>
            WaitForCustomStatusAsync(daprWorkflowClient, id, WaitingStatus, waitCts.Token)));

        // All three are now suspended. Schedule the fourth, which should not be blocked.
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(EchoWorkflow), fourthId, fourthId);

        using var fourthCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var fourthResult = await daprWorkflowClient.WaitForWorkflowCompletionAsync(
            fourthId, getInputsAndOutputs: true, cancellation: fourthCts.Token);

        Assert.Equal(WorkflowRuntimeStatus.Completed, fourthResult.RuntimeStatus);
        Assert.Equal(fourthId, fourthResult.ReadOutputAs<string>());

        // Release all three waiting workflows.
        await Task.WhenAll(waitingIds.Select(id =>
            daprWorkflowClient.RaiseEventAsync(id, EventName, "released",
                TestContext.Current.CancellationToken)));

        using var completionCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var waitingResults = await Task.WhenAll(waitingIds.Select(id =>
            daprWorkflowClient.WaitForWorkflowCompletionAsync(
                id, getInputsAndOutputs: true, cancellation: completionCts.Token)));

        foreach (var result in waitingResults)
        {
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal("released", result.ReadOutputAs<string>());
        }
    }

    private static async Task WaitForCustomStatusAsync(
        DaprWorkflowClient client,
        string instanceId,
        string expectedStatus,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await client.GetWorkflowStateAsync(
                instanceId, getInputsAndOutputs: true, cancellation: cancellationToken);
            if (state is not null &&
                string.Equals(state.ReadCustomStatusAs<string>(), expectedStatus, StringComparison.Ordinal))
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    /// <summary>Waits indefinitely for an external event then returns its payload.</summary>
    private sealed class WaitForEventWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input)
        {
            context.SetCustomStatus(WaitingStatus);
            return await context.WaitForExternalEventAsync<string>(EventName);
        }
    }

    private sealed class EchoActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input) =>
            Task.FromResult(input);
    }

    private sealed class EchoWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input) =>
            await context.CallActivityAsync<string>(nameof(EchoActivity), input);
    }
}
