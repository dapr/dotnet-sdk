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

public sealed class ParallelExecutionTests
{
    [Fact]
    public async Task ShouldExecuteActivitiesInParallel()
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
                    opt =>
                    {
                        opt.RegisterWorkflow<ParallelWorkflow>();
                        opt.RegisterActivity<DelayedActivity>();
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

        var startTime = DateTime.UtcNow;
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(ParallelWorkflow), workflowInstanceId);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        var endTime = DateTime.UtcNow;

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var outputs = result.ReadOutputAs<List<string>>();
        Assert.NotNull(outputs);
        Assert.Equal(3, outputs.Count);
        
        // Should complete in ~2 seconds (parallel) not 6 seconds (sequential)
        var duration = endTime - startTime;
        Assert.True(duration.TotalSeconds < 5, $"Expected parallel execution to complete in < 5 seconds, took {duration.TotalSeconds}");
    }

    [Fact]
    public async Task ShouldFanOutFanInWithActivities()
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
                    opt =>
                    {
                        opt.RegisterWorkflow<FanOutFanInWorkflow>();
                        opt.RegisterActivity<SquareActivity>();
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(FanOutFanInWorkflow), workflowInstanceId, (int[])[1, 2, 3, 4
        ]);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var sum = result.ReadOutputAs<int>();
        Assert.Equal(30, sum); // 1² + 2² + 3² + 4² = 1 + 4 + 9 + 16 = 30
    }

    private sealed class ParallelWorkflow : Workflow<object?, List<string>>
    {
        public override async Task<List<string>> RunAsync(WorkflowContext context, object? input)
        {
            var tasks = new List<Task<string>>
            {
                context.CallActivityAsync<string>(nameof(DelayedActivity), "Task1"),
                context.CallActivityAsync<string>(nameof(DelayedActivity), "Task2"),
                context.CallActivityAsync<string>(nameof(DelayedActivity), "Task3")
            };

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }
    }

    private sealed class FanOutFanInWorkflow : Workflow<int[], int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int[] input)
        {
            var tasks = input.Select(num => context.CallActivityAsync<int>(nameof(SquareActivity), num)).ToList();
            var results = await Task.WhenAll(tasks);
            return results.Sum();
        }
    }

    private sealed class DelayedActivity : WorkflowActivity<string, string>
    {
        public override async Task<string> RunAsync(WorkflowActivityContext context, string input)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            return $"Completed: {input}";
        }
    }

    private sealed class SquareActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            return Task.FromResult(input * input);
        }
    }
}
