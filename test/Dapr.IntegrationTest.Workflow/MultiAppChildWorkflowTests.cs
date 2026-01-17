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

public sealed class MultiAppChildWorkflowTests
{
    [Fact]
    public async Task ShouldScheduleChildWorkflowOnRemoteAppUsingAppId()
    {
        var guid = Guid.NewGuid().ToString("N");
        var app1Id = $"workflow-app-1-{guid}";
        var app2Id = $"workflow-app-2-{guid}";

        var options1 = new DaprRuntimeOptions();
        options1.WithAppId(app1Id);
        var options2 = new DaprRuntimeOptions();
        options2.WithAppId(app2Id);

        var componentsDir1 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-1");
        var componentsDir2 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-2");
        
        // Build our shared environment (Network + Control plane)
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();
        
        // Build and start the first application
        var harness1 = new DaprHarnessBuilder(options1, environment).BuildWorkflow(componentsDir1);
        await using var app1 = await DaprHarnessBuilder.ForHarness(harness1)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        // App1 only registers the initiating workflow
                        opt.RegisterWorkflow<InitialWorkflow>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            }).BuildAndStartAsync();
        
        // Build and start the second application (target)
        var harness2 = new DaprHarnessBuilder(options2, environment).BuildWorkflow(componentsDir2);
        await using var app2 = await DaprHarnessBuilder.ForHarness(harness2)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<TargetWorkflow>();
                        opt.RegisterActivity<MultiplyActivity>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            }).BuildAndStartAsync();

        using var scope1 = app1.CreateScope();
        var client1 = scope1.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
       
        const int inputValue = 7;
        var initialWorkflowId = $"initial-workflow-instance-{guid}";
        var targetWorkflowId = $"remote-workflow-instance-{guid}";
        
        // Start the initial workflow on App1
        await client1.ScheduleNewWorkflowAsync(nameof(InitialWorkflow), initialWorkflowId,
            new InitialWorkflowInput(app2Id, targetWorkflowId, inputValue));
        
        // Wait for the initiator workflow to complete
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var initialResult = await client1.WaitForWorkflowCompletionAsync(initialWorkflowId, cancellation: cts.Token);
        if (cts.Token.IsCancellationRequested)
        {
            Assert.Fail("Waiting for first workflow completion timed out");
        }
        Assert.Equal(WorkflowRuntimeStatus.Completed, initialResult.RuntimeStatus);
        
        // Verify the target workflow on App2 also completed successfully
        using var scope2 = app2.CreateScope();
        var client2 = scope2.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        cts.TryReset();
        var targetResult = await client2.WaitForWorkflowCompletionAsync(targetWorkflowId, cancellation: cts.Token);
        if (cts.Token.IsCancellationRequested)
        {
            Assert.Fail("Waiting for second workflow completion timed out");
        }
        
        Assert.Equal(WorkflowRuntimeStatus.Completed, targetResult.RuntimeStatus);
        var targetOutput = targetResult.ReadOutputAs<int>();
        Assert.Equal(inputValue * 3, targetOutput); // Activity multiplies by 3
    }
    
    private sealed record InitialWorkflowInput(string TargetAppId, string TargetWorkflowId, int Value);

    private sealed class InitialWorkflow : Workflow<InitialWorkflowInput, object?>
    {
        public override async Task<object?> RunAsync(WorkflowContext context, InitialWorkflowInput input)
        {
            try
            {
                // Schedule a child workflow running on another app
                var workflowResult = await context.CallChildWorkflowAsync<int>(nameof(TargetWorkflow), input.Value,
                    new ChildWorkflowTaskOptions { InstanceId = input.TargetWorkflowId, TargetAppId = input.TargetAppId });
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
    }

    private sealed class TargetWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input) =>
            // Perform some work via an activity
            await context.CallActivityAsync<int>(nameof(MultiplyActivity), input);
    }

    private sealed class MultiplyActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) => Task.FromResult(input * 3);
    }
}
