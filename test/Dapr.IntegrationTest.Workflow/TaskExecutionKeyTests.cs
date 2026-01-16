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

public sealed class TaskExecutionKeyTests
{
    [Fact]
    public async Task ActivityContext_ShouldContainTaskExecutionKey()
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
                        opt.RegisterWorkflow<GetTaskExecutionKeyWorkflow>();
                        opt.RegisterActivity<GetTaskExecutionKeyActivity>();
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
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(GetTaskExecutionKeyWorkflow), workflowInstanceId, "start");
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId, true);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        
        // Retrieve the task execution key returned by the activity
        var taskExecutionKey = result.ReadOutputAs<string>();
        
        // Assert that the TaskExecutionKey is present
        Assert.False(string.IsNullOrWhiteSpace(taskExecutionKey), "TaskExecutionKey should not be null or empty.");
    }
    
    private sealed class GetTaskExecutionKeyActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        {
            // Verify we can access the TaskExecutionKey from the context
            return Task.FromResult(context.TaskExecutionKey);
        }
    }

    private sealed class GetTaskExecutionKeyWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input)
        {
            // Call the activity and return its result (the TaskExecutionKey)
            var result = await context.CallActivityAsync<string>(nameof(GetTaskExecutionKeyActivity), input);
            return result;
        }
    }
}
