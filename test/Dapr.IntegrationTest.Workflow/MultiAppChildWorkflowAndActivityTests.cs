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

using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class MultiAppChildWorkflowAndActivityTests
{
    [Fact]
    public async Task ShouldScheduleChildWorkflowOnRemoteApp_ThatCallsActivityOnAnotherRemoteApp_UsingAppIds()
    {
        const string app1Id = "workflow-app-1";
        const string app2Id = "workflow-app-2";
        const string app3Id = "workflow-app-3";

        var options1 = new DaprRuntimeOptions().WithAppId(app1Id);
        var options2 = new DaprRuntimeOptions().WithAppId(app2Id);
        var options3 = new DaprRuntimeOptions().WithAppId(app3Id);

        var componentsDir1 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-chain-1");
        var componentsDir2 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-chain-2");
        var componentsDir3 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-chain-3");

        await using var environment = new DaprTestEnvironment(needsActorState: true);
        await environment.StartAsync();

        // App1: initiator (calls child workflow on App2)
        var harness1 = new DaprHarnessBuilder(options1, environment).BuildWorkflow(componentsDir1);
        await using var app1 = await DaprHarnessBuilder.ForHarness(harness1)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<InitialWorkflow>();
                        opt.AppId = app1Id;
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

        // App2: child workflow host (calls activity on App3)
        var harness2 = new DaprHarnessBuilder(options2, environment).BuildWorkflow(componentsDir2);
        await using var app2 = await DaprHarnessBuilder.ForHarness(harness2)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<ChildWorkflow>();
                        opt.AppId = app2Id;
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

        // App3: activity host
        var harness3 = new DaprHarnessBuilder(options3, environment).BuildWorkflow(componentsDir3);
        await using var app3 = await DaprHarnessBuilder.ForHarness(harness3)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterActivity<MultiplyByThreeActivity>();
                        opt.AppId = app3Id;
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

        using var scope1 = app1.CreateScope();
        var client1 = scope1.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        const int inputValue = 7;
        var runId = Guid.NewGuid().ToString("N");

        var initialWorkflowId = $"initial-workflow-instance-{runId}";
        var childWorkflowId = $"child-workflow-instance-{runId}";

        await client1.ScheduleNewWorkflowAsync(
            nameof(InitialWorkflow),
            initialWorkflowId,
            new InitialWorkflowInput(
                ChildWorkflowTargetAppId: app2Id,
                ChildWorkflowInstanceId: childWorkflowId,
                ActivityTargetAppId: app3Id,
                Value: inputValue));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        // App1 workflow should complete and return the activity output (via the child workflow)
        var initialResult = await client1.WaitForWorkflowCompletionAsync(initialWorkflowId, cancellation: cts.Token);
        if (cts.Token.IsCancellationRequested)
        {
            Assert.Fail("Waiting for initial workflow completion timed out");
        }

        Assert.Equal(WorkflowRuntimeStatus.Completed, initialResult.RuntimeStatus);
        var output = initialResult.ReadOutputAs<int>();
        Assert.Equal(inputValue * 3, output);

        // Also verify the child workflow on App2 completed successfully
        using var scope2 = app2.CreateScope();
        var client2 = scope2.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        cts.TryReset();
        var childResult = await client2.WaitForWorkflowCompletionAsync(childWorkflowId, cancellation: cts.Token);
        if (cts.Token.IsCancellationRequested)
        {
            Assert.Fail("Waiting for child workflow completion timed out");
        }

        Assert.Equal(WorkflowRuntimeStatus.Completed, childResult.RuntimeStatus);
        var childOutput = childResult.ReadOutputAs<int>();
        Assert.Equal(inputValue * 3, childOutput);
    }

    private sealed record InitialWorkflowInput(
        string ChildWorkflowTargetAppId,
        string ChildWorkflowInstanceId,
        string ActivityTargetAppId,
        int Value);

    private sealed class InitialWorkflow : Workflow<InitialWorkflowInput, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, InitialWorkflowInput input) =>
            context.CallChildWorkflowAsync<int>(
                nameof(ChildWorkflow),
                input.Value,
                new ChildWorkflowTaskOptions(
                    InstanceId: input.ChildWorkflowInstanceId,
                    TargetAppId: input.ChildWorkflowTargetAppId));
    }

    private sealed class ChildWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) =>
            context.CallActivityAsync<int>(
                nameof(MultiplyByThreeActivity),
                input,
                new WorkflowTaskOptions(TargetAppId: ActivityTargetAppIdHolder.TargetAppId));
    }

    private static class ActivityTargetAppIdHolder
    {
        // This gets set per workflow execution (see below) so the child workflow can route the activity call.
        // The Dapr Workflow runtime invokes workflows in-process; this is safe for this test because the app hosts
        // only this workflow and we run one instance per test method.
        public static string TargetAppId { get; set; } = string.Empty;
    }

    private sealed class MultiplyByThreeActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) => Task.FromResult(input * 3);
    }

    // Wire the activity target app ID into the child workflow in a deterministic way.
    // (We avoid relying on ambient config inside the workflow itself.)
    static MultiAppChildWorkflowAndActivityTests()
    {
        ActivityTargetAppIdHolder.TargetAppId = "workflow-app-3";
    }
}
