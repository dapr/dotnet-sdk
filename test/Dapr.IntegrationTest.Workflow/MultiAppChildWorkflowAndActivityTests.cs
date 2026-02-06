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

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class MultiAppChildWorkflowAndActivityTests
{
    private static readonly string UniqueId = Guid.NewGuid().ToString("N");
    private static readonly string App1Id = $"workflow-app-1-{UniqueId}";
    private static readonly string App2Id = $"workflow-app-2-{UniqueId}";
    private static readonly string App3Id = $"workflow-app-3-{UniqueId}";
    
    [Fact]
    public async Task ShouldScheduleChildWorkflowOnRemoteApp_ThatCallsActivityOnAnotherRemoteApp_UsingAppIds()
    {
        var options1 = new DaprRuntimeOptions().WithAppId(App1Id);
        var options2 = new DaprRuntimeOptions().WithAppId(App2Id);
        var options3 = new DaprRuntimeOptions().WithAppId(App3Id);

        var componentsDir1 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-chain-1");
        var componentsDir2 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-chain-2");
        var componentsDir3 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-chain-3");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        // App1: initiator (calls child workflow on App2)
        var harness1 = new DaprHarnessBuilder(componentsDir1)
            .WithOptions(options1)
            .WithEnvironment(environment)
            .BuildWorkflow();
        await using var app1 = await DaprHarnessBuilder.ForHarness(harness1)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<InitialWorkflow>();
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
        var harness2 = new DaprHarnessBuilder(componentsDir2)
            .WithEnvironment(environment)
            .WithOptions(options2)
            .BuildWorkflow();
        await using var app2 = await DaprHarnessBuilder.ForHarness(harness2)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<ChildWorkflow>();
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
        var harness3 = new DaprHarnessBuilder(componentsDir3)
            .WithEnvironment(environment)
            .WithOptions(options3)
            .BuildWorkflow();
        await using var app3 = await DaprHarnessBuilder.ForHarness(harness3)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterActivity<MultiplyByThreeActivity>();
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
        var initialWorkflowId = $"initial-workflow-instance-{UniqueId}";
        var childWorkflowId = $"child-workflow-instance-{UniqueId}";

        await client1.ScheduleNewWorkflowAsync(
            nameof(InitialWorkflow),
            initialWorkflowId,
            new InitialWorkflowInput(
                ChildWorkflowTargetAppId: App2Id,
                ChildWorkflowInstanceId: childWorkflowId,
                ActivityTargetAppId: App3Id,
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

    // Hosted in App1
    private sealed class InitialWorkflow : Workflow<InitialWorkflowInput, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, InitialWorkflowInput input) =>
            context.CallChildWorkflowAsync<int>(
                nameof(ChildWorkflow),
                new ChildWorkflowInput(input.ActivityTargetAppId, input.Value),
                new ChildWorkflowTaskOptions(
                    InstanceId: input.ChildWorkflowInstanceId,
                    TargetAppId: input.ChildWorkflowTargetAppId));
    }
    private sealed record InitialWorkflowInput(
        string ChildWorkflowTargetAppId,
        string ChildWorkflowInstanceId,
        string ActivityTargetAppId,
        int Value);

    // Hosted in App2
    private sealed class ChildWorkflow : Workflow<ChildWorkflowInput, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, ChildWorkflowInput input) =>
            context.CallActivityAsync<int>(
                nameof(MultiplyByThreeActivity),
                input.Value,
                new WorkflowTaskOptions(TargetAppId: input.ActivityTargetAppId));
    }
    private sealed record ChildWorkflowInput(
        string ActivityTargetAppId,
        int Value);
    
    // Hosted in App3
    private sealed class MultiplyByThreeActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) => Task.FromResult(input * 3);
    }
}
