// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class DetachedWorkflowTests
{
    [MinimumDaprRuntimeFact("1.19")]
    public async Task DetachedWorkflowSurvivesRecursiveParentTermination()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("detached-workflow-components");
        var parentInstanceId = Guid.NewGuid().ToString();
        var detachedInstanceId = Guid.NewGuid().ToString();

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
                    configureRuntime: options =>
                    {
                        options.RegisterWorkflow<ParentWorkflow>();
                        options.RegisterWorkflow<DetachedWorkflow>();
                    },
                    configureClient: (serviceProvider, clientBuilder) =>
                    {
                        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = configuration["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                        {
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                        }
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(
            nameof(ParentWorkflow), parentInstanceId, detachedInstanceId);

        var detachedStarted = await client.WaitForWorkflowStartAsync(
            detachedInstanceId, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowRuntimeStatus.Running, detachedStarted.RuntimeStatus);

        await client.RaiseEventAsync(
            parentInstanceId, "continue", null, TestContext.Current.CancellationToken);
        await WaitForCustomStatusAsync(client, parentInstanceId, "replayed");

        await client.TerminateWorkflowAsync(
            parentInstanceId, cancellation: TestContext.Current.CancellationToken);
        var parent = await client.WaitForWorkflowCompletionAsync(
            parentInstanceId, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowRuntimeStatus.Terminated, parent.RuntimeStatus);

        var detachedAfterParentTermination = await client.GetWorkflowStateAsync(
            detachedInstanceId, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowRuntimeStatus.Running, detachedAfterParentTermination.RuntimeStatus);

        await client.RaiseEventAsync(
            detachedInstanceId, "complete", "detached-result", TestContext.Current.CancellationToken);
        var detached = await client.WaitForWorkflowCompletionAsync(
            detachedInstanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, detached.RuntimeStatus);
        Assert.Equal("detached-result", detached.ReadOutputAs<string>());
    }

    private static async Task WaitForCustomStatusAsync(
        DaprWorkflowClient client,
        string instanceId,
        string expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token, TestContext.Current.CancellationToken);

        while (true)
        {
            var state = await client.GetWorkflowStateAsync(
                instanceId, cancellation: linked.Token);
            if (state.ReadCustomStatusAs<string>() == expected)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), linked.Token);
        }
    }

    private sealed class ParentWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string detachedInstanceId)
        {
            await context.ScheduleNewDetachedWorkflowAsync(
                nameof(DetachedWorkflow), detachedInstanceId);
            await context.WaitForExternalEventAsync<object?>("continue");
            context.SetCustomStatus("replayed");
            await context.WaitForExternalEventAsync<string>("never");
            return "parent-completed";
        }
    }

    private sealed class DetachedWorkflow : Workflow<object?, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, object? input) =>
            context.WaitForExternalEventAsync<string>("complete");
    }
}
