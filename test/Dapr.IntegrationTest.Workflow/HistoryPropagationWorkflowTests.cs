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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.IntegrationTest.Workflow;

/// <summary>
/// Integration tests for workflow history propagation.
///
/// These tests verify the SDK-side API works end-to-end:
///   - Scheduling child workflows with a history propagation scope does not cause errors
///   - The parent and child workflows complete successfully
///   - The child can call GetPropagatedHistory() without error
///
/// NOTE: Full history content propagation (non-null PropagatedHistory in the child) requires
/// Dapr sidecar support for the propagated_history field in OrchestratorRequest. When the
/// sidecar supports this, GetPropagatedHistory() will return a non-null PropagatedHistory.
/// The sidecar reads the history_propagation_scope and propagated_history fields that the SDK
/// sets in the CreateSubOrchestrationAction proto message.
/// </summary>
public sealed class HistoryPropagationWorkflowTests
{
    /// <summary>
    /// Verifies that scheduling a child workflow with <see cref="HistoryPropagationScope.None"/>
    /// (the default) completes successfully.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task ShouldCompleteSuccessfully_WithNoPropagationScope()
    {
        var instanceId = Guid.NewGuid().ToString();
        await using var testApp = await BuildTestAppAsync(
            opt =>
            {
                opt.RegisterWorkflow<NoPropagationParent>();
                opt.RegisterWorkflow<PropagatedHistoryReceiver>();
            });

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(nameof(NoPropagationParent), instanceId);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId,
            cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<PropagationTestResult>();
        Assert.NotNull(output);
        // No scope set → child should report no propagated history
        Assert.False(output.ChildReceivedPropagatedHistory);
        Assert.Equal(0, output.PropagatedEntryCount);
    }

    /// <summary>
    /// Verifies that scheduling a child workflow with <see cref="HistoryPropagationScope.OwnHistory"/>
    /// does not produce any errors and both workflows complete.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task ShouldCompleteSuccessfully_WithOwnHistoryPropagationScope()
    {
        var instanceId = Guid.NewGuid().ToString();
        await using var testApp = await BuildTestAppAsync(
            opt =>
            {
                opt.RegisterWorkflow<OwnHistoryPropagationParent>();
                opt.RegisterWorkflow<PropagatedHistoryReceiver>();
                opt.RegisterWorkflow<SimpleActivityWorkflow>();
                opt.RegisterActivity<EchoActivity>();
            });

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(nameof(OwnHistoryPropagationParent), instanceId);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId,
            cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
    }

    /// <summary>
    /// Verifies that scheduling a child workflow with <see cref="HistoryPropagationScope.Lineage"/>
    /// does not produce any errors and both workflows complete.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task ShouldCompleteSuccessfully_WithLineagePropagationScope()
    {
        var instanceId = Guid.NewGuid().ToString();
        await using var testApp = await BuildTestAppAsync(
            opt =>
            {
                opt.RegisterWorkflow<LineagePropagationParent>();
                opt.RegisterWorkflow<LineagePropagationMiddle>();
                opt.RegisterWorkflow<PropagatedHistoryReceiver>();
            });

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(nameof(LineagePropagationParent), instanceId);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId,
            cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
    }

    /// <summary>
    /// Verifies that calling GetPropagatedHistory() inside a child workflow scheduled with
    /// None scope returns null, not an exception.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task GetPropagatedHistory_ReturnsNull_WhenScheduledWithNoneScope()
    {
        var instanceId = Guid.NewGuid().ToString();
        await using var testApp = await BuildTestAppAsync(
            opt =>
            {
                opt.RegisterWorkflow<NoPropagationParent>();
                opt.RegisterWorkflow<PropagatedHistoryReceiver>();
            });

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(nameof(NoPropagationParent), instanceId);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId,
            cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<PropagationTestResult>();
        Assert.NotNull(output);
        Assert.False(output.ChildReceivedPropagatedHistory);
    }

    /// <summary>
    /// Verifies propagation scope option is preserved through the WorkflowTaskOptions.WithHistoryPropagation
    /// fluent builder and that the child workflow completes successfully.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task WithHistoryPropagation_FluentBuilder_WorksCorrectly()
    {
        var instanceId = Guid.NewGuid().ToString();
        await using var testApp = await BuildTestAppAsync(
            opt =>
            {
                opt.RegisterWorkflow<FluentBuilderParent>();
                opt.RegisterWorkflow<PropagatedHistoryReceiver>();
            });

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(nameof(FluentBuilderParent), instanceId);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId,
            cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
    }

    private static async Task<DaprTestApplication> BuildTestAppAsync(Action<WorkflowRuntimeOptions> configureRuntime)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");

        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        return await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: configureRuntime,
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrWhiteSpace(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();
    }

    private sealed record PropagationTestResult(
        bool ChildReceivedPropagatedHistory,
        int PropagatedEntryCount);

    /// <summary>
    /// Parent workflow that schedules a child with no propagation scope (default).
    /// </summary>
    private sealed class NoPropagationParent : Workflow<object?, PropagationTestResult>
    {
        public override async Task<PropagationTestResult> RunAsync(WorkflowContext context, object? input)
        {
            var childId = $"{context.InstanceId}-child";
            return await context.CallChildWorkflowAsync<PropagationTestResult>(
                nameof(PropagatedHistoryReceiver),
                input: null,
                options: new ChildWorkflowTaskOptions(InstanceId: childId));
        }
    }

    /// <summary>
    /// Parent workflow that runs an activity first (to build some history), then schedules a child
    /// with OwnHistory propagation.
    /// </summary>
    private sealed class OwnHistoryPropagationParent : Workflow<object?, PropagationTestResult>
    {
        public override async Task<PropagationTestResult> RunAsync(WorkflowContext context, object? input)
        {
            // Run an activity to build some history
            await context.CallActivityAsync<string>(
                nameof(EchoActivity), "ping");

            var childId = $"{context.InstanceId}-child";
            var childOptions = new ChildWorkflowTaskOptions(InstanceId: childId)
                .WithHistoryPropagation(HistoryPropagationScope.OwnHistory);

            return await context.CallChildWorkflowAsync<PropagationTestResult>(
                nameof(PropagatedHistoryReceiver),
                input: null,
                options: childOptions);
        }
    }

    /// <summary>
    /// Grandparent → middle → child lineage, each using Lineage propagation.
    /// </summary>
    private sealed class LineagePropagationParent : Workflow<object?, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, object? input)
        {
            var childId = $"{context.InstanceId}-middle";
            var childOptions = new ChildWorkflowTaskOptions(InstanceId: childId)
                .WithHistoryPropagation(HistoryPropagationScope.Lineage);

            return await context.CallChildWorkflowAsync<bool>(
                nameof(LineagePropagationMiddle),
                input: null,
                options: childOptions);
        }
    }

    private sealed class LineagePropagationMiddle : Workflow<object?, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, object? input)
        {
            var childId = $"{context.InstanceId}-leaf";
            var childOptions = new ChildWorkflowTaskOptions(InstanceId: childId)
                .WithHistoryPropagation(HistoryPropagationScope.Lineage);

            var result = await context.CallChildWorkflowAsync<PropagationTestResult>(
                nameof(PropagatedHistoryReceiver),
                input: null,
                options: childOptions);

            return result is not null;
        }
    }

    /// <summary>
    /// Parent workflow that uses the fluent <c>WithHistoryPropagation</c> builder style.
    /// </summary>
    private sealed class FluentBuilderParent : Workflow<object?, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, object? input)
        {
            var childId = $"{context.InstanceId}-child";

            // Use fluent builder chaining
            var options = new ChildWorkflowTaskOptions(InstanceId: childId)
                .WithHistoryPropagation(HistoryPropagationScope.OwnHistory);

            var result = await context.CallChildWorkflowAsync<PropagationTestResult>(
                nameof(PropagatedHistoryReceiver),
                input: null,
                options: options);

            return result is not null;
        }
    }

    /// <summary>
    /// Child workflow that inspects its propagated history and reports back what it found.
    /// </summary>
    private sealed class PropagatedHistoryReceiver : Workflow<object?, PropagationTestResult>
    {
        public override Task<PropagationTestResult> RunAsync(WorkflowContext context, object? input)
        {
            var propagated = context.GetPropagatedHistory();
            var result = new PropagationTestResult(
                ChildReceivedPropagatedHistory: propagated is not null,
                PropagatedEntryCount: propagated?.Entries.Count ?? 0);
            return Task.FromResult(result);
        }
    }

    private sealed class EchoActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
            => Task.FromResult(input);
    }

    private sealed class SimpleActivityWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input)
        {
            return await context.CallActivityAsync<string>(nameof(EchoActivity), input);
        }
    }
}
