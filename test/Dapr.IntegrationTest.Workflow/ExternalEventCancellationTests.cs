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

public sealed class ExternalEventCancellationSequentialTests
{
    [Fact]
    public async Task ExternalEvents_ShouldComplete_WhenRaisedSequentially_WithDelay()
    {
        await ExternalEventCancellationTestHarness.RunAsync(
            workflowCount: 1000,
            raiseEventsInParallel: false,
            perEventDelay: TimeSpan.FromMilliseconds(75),
            initialWaitTimeout: TimeSpan.FromMilliseconds(200));
    }
}

public sealed class ExternalEventCancellationParallelTests
{
    [Fact]
    public async Task ExternalEvents_ShouldComplete_WhenRaisedInParallel_MinimalDelay()
    {
        await ExternalEventCancellationTestHarness.RunAsync(
            workflowCount: 1000,
            raiseEventsInParallel: true,
            perEventDelay: TimeSpan.Zero,
            initialWaitTimeout: TimeSpan.FromMilliseconds(200));
    }
}

internal static class ExternalEventCancellationTestHarness
{
    private const string EventName = "SemaphoreSignal";
    private const string WaitingAfterTimeoutStatus = "WaitingAfterTimeout";

    public static async Task RunAsync(
        int workflowCount,
        bool raiseEventsInParallel,
        TimeSpan perEventDelay,
        TimeSpan initialWaitTimeout)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowIds = Enumerable.Range(0, workflowCount)
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<CanceledWaitWorkflow>();
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

        foreach (var workflowId in workflowIds)
        {
            await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(CanceledWaitWorkflow), workflowId, initialWaitTimeout);
        }

        using var waitCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await Task.WhenAll(workflowIds.Select(id =>
            WaitForCustomStatusAsync(daprWorkflowClient, id, WaitingAfterTimeoutStatus, waitCts.Token)));

        if (raiseEventsInParallel)
        {
            var raiseTasks = workflowIds.Select(id =>
                daprWorkflowClient.RaiseEventAsync(id, EventName, "released"));
            await Task.WhenAll(raiseTasks);
        }
        else
        {
            foreach (var workflowId in workflowIds)
            {
                await daprWorkflowClient.RaiseEventAsync(workflowId, EventName, "released");
                if (perEventDelay > TimeSpan.Zero)
                    await Task.Delay(perEventDelay);
            }
        }

        using var completionCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var results = await Task.WhenAll(workflowIds.Select(id =>
            daprWorkflowClient.WaitForWorkflowCompletionAsync(id, cancellation: completionCts.Token)));

        foreach (var result in results)
        {
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            var payload = result.ReadOutputAs<string>();
            Assert.Equal("released", payload);
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
            var state = await client.GetWorkflowStateAsync(instanceId, getInputsAndOutputs: true, cancellation: cancellationToken);
            if (state is not null && string.Equals(state.ReadCustomStatusAs<string>(), expectedStatus, StringComparison.Ordinal))
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    private sealed class CanceledWaitWorkflow : Workflow<TimeSpan, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, TimeSpan initialWaitTimeout)
        {
            try
            {
                context.SetCustomStatus("WaitingWithTimeout");
                await context.WaitForExternalEventAsync<string>(EventName, initialWaitTimeout);
                return "unexpected";
            }
            catch (TaskCanceledException)
            {
                context.SetCustomStatus(WaitingAfterTimeoutStatus);
            }

            var result = await context.WaitForExternalEventAsync<string>(EventName);
            return result;
        }
    }
}
