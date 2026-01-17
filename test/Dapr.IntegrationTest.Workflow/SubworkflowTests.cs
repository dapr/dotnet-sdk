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

public sealed class SubworkflowTests
{
    [Fact]
    public async Task ShouldHandleSubworkflow()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();
        
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(options, environment).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<DemoWorkflow>();
                        opt.RegisterWorkflow<DemoSubWorkflow>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrWhiteSpace(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(DemoWorkflow), workflowInstanceId, workflowInstanceId);
        
        var workflowResult = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, workflowResult.RuntimeStatus);
        var workflowResultValue = workflowResult.ReadOutputAs<bool>();
        Assert.True(workflowResultValue);

        var subworkflowResult = await daprWorkflowClient.WaitForWorkflowCompletionAsync($"{workflowInstanceId}-sub");
        Assert.Equal(WorkflowRuntimeStatus.Completed, workflowResult.RuntimeStatus);
        var subworkflowResultValue = subworkflowResult.ReadOutputAs<bool>();
        Assert.True(subworkflowResultValue);
    }
    
    [Fact]
    public async Task ShouldHandleMultipleParallelSubworkflows()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();
        
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(options, environment).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<ParallelSubworkflowsWorkflow>();
                        opt.RegisterWorkflow<ProcessingSubworkflow>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrWhiteSpace(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(
            nameof(ParallelSubworkflowsWorkflow), 
            workflowInstanceId);

        var workflowResult = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, workflowResult.RuntimeStatus);
        var results = workflowResult.ReadOutputAs<int[]>();
        Assert.NotNull(results);
        Assert.Equal(3, results.Length);
        Assert.Equal([10, 20, 30], results);
    }

    private sealed class ParallelSubworkflowsWorkflow : Workflow<object?, int[]>
    {
        public override async Task<int[]> RunAsync(WorkflowContext context, object? input)
        {
            var tasks = new List<Task<int>>();
            
            for (int i = 1; i <= 3; i++)
            {
                var subInstanceId = $"{context.InstanceId}-sub-{i}";
                var options = new ChildWorkflowTaskOptions(subInstanceId);
                var task = context.CallChildWorkflowAsync<int>(
                    nameof(ProcessingSubworkflow), 
                    i * 10, 
                    options);
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            return results;
        }
    }

    private sealed class ProcessingSubworkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            await context.CreateTimer(TimeSpan.FromSeconds(2));
            return input;
        }
    }

    private sealed class DemoWorkflow : Workflow<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, string instanceId)
        {
            var subInstanceId = $"{instanceId}-sub";
            var options = new ChildWorkflowTaskOptions(subInstanceId);
            await context.CallChildWorkflowAsync<bool>(nameof(DemoSubWorkflow), "Hello, sub-workflow", options);
            return true;
        }
    }

    private sealed class DemoSubWorkflow : Workflow<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, string input)
        {
            await context.CreateTimer(TimeSpan.FromSeconds(5));
            return true;
        }
    }
}
