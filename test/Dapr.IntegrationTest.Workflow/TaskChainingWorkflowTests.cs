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

using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class TaskChainingWorkflowTests
{
    private static readonly int[] expected = [43, 45, 90];

    [Fact]
    public async Task ShouldHandleTaskChaining()
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
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<TestWorkflow>();
                        opt.RegisterActivity<Step1Activity>();
                        opt.RegisterActivity<Step2Activity>();
                        opt.RegisterActivity<Step3Activity>();
                    }, configureClient: (sp, clientBuilder) =>
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
        
        // Start the workflow
        const int startingValue = 42;

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TestWorkflow), workflowInstanceId, startingValue);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var resultValue = result.ReadOutputAs<int[]>() ?? [];
        Assert.Equal(expected, resultValue);
    }

    private sealed class TestWorkflow : Workflow<int, int[]>
    {
        public override async Task<int[]> RunAsync(WorkflowContext context, int input)
        {
            var result1 = await context.CallActivityAsync<int>(nameof(Step1Activity), input);
            var result2 = await context.CallActivityAsync<int>(nameof(Step2Activity), result1);
            var result3 = await context.CallActivityAsync<int>(nameof(Step3Activity), result2);
            var ret = new[] { result1, result2, result3 };

            return ret;
        }
    }

    private sealed class Step1Activity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            return Task.FromResult(input + 1);
        }
    }

    private sealed class Step2Activity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            return Task.FromResult(input + 2);
        }
    }
    
    private sealed class Step3Activity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            return Task.FromResult(input * 2);
        }
    }
}

