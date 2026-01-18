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

public sealed class ActivitySleepTests
{
    [Fact]
    public async Task ShouldHandleActivitySleep()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId1 = Guid.NewGuid().ToString();
        var workflowInstanceId2 = Guid.NewGuid().ToString();

        // Build the environment
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
                        opt.RegisterWorkflow<Test1Workflow>();
                        opt.RegisterWorkflow<Test2Workflow>();
                        opt.RegisterActivity<SleepActivity>();
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

        // Clean test logic
        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        // Start the workflow
        const int startingValue = 8;

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(Test1Workflow), workflowInstanceId1, startingValue);

        var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(Test2Workflow), workflowInstanceId2, startingValue, null, cts1.Token);

        var state = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId1);
        Assert.NotNull(state);
        Assert.Equal(WorkflowRuntimeStatus.Running, state.RuntimeStatus);

        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId2, true, cts2.Token);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var resultValue = result.ReadOutputAs<int>();
        Assert.Equal(9, resultValue);
    }

    private sealed class SleepActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            Thread.Sleep(int.MaxValue);
            return Task.FromResult(input);
        }
    }

    private sealed class Test1Workflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            await context.CallActivityAsync(nameof(SleepActivity), input);
            return 0;
        }
    }

    private sealed class Test2Workflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input)
        {
            return Task.FromResult(input + 1);
        }
    }
}
