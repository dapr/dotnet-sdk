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
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class TimerTests
{
    private const string InitialMessage = "Test1";
    private const string FinalMessage = "Test2";
    
    [Fact]
    public async Task ValidateStatusMessagesWithDelay()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();
        
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt =>
                    {
                        opt.RegisterWorkflow<TestWorkflow>();
                        opt.RegisterActivity<TestActivity>();
                    },
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
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TestWorkflow), workflowInstanceId, 8);
        await Task.Delay(TimeSpan.FromSeconds(3));
        
        // Get the initial status
        var initialStatus = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId);
        Assert.NotNull(initialStatus);
        Assert.Equal(WorkflowRuntimeStatus.Running, initialStatus.RuntimeStatus);
        var initialStatusResult = initialStatus.ReadCustomStatusAs<string>();
        Assert.Equal(InitialMessage, initialStatusResult);
        
        // Wait 20 seconds
        await Task.Delay(TimeSpan.FromSeconds(20));
        
        // Get the current status
        var finalStatus = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId);
        Assert.NotNull(finalStatus);
        var finalStatusResult = finalStatus.ReadCustomStatusAs<string>();
        Assert.Equal(FinalMessage, finalStatusResult);
        Assert.Equal(WorkflowRuntimeStatus.Completed, finalStatus.RuntimeStatus);
    }

    private sealed class TestWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            context.SetCustomStatus(InitialMessage);
            await context.CreateTimer(TimeSpan.FromSeconds(15));
            context.SetCustomStatus(FinalMessage);
            return await context.CallActivityAsync<int>(nameof(TestActivity), input);
        }
    }

    private sealed class TestActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) => Task.FromResult(input * 2);
    }
}
