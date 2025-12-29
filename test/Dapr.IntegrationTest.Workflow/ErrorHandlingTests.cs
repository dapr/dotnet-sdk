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
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task ShouldRetryFailedActivityAndSucceed()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt =>
                    {
                        opt.RegisterWorkflow<RetryWorkflow>();
                        opt.RegisterActivity<FlakyActivity>();
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(RetryWorkflow), workflowInstanceId, 0);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
    
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<string>();
        Assert.Equal("Success after retries", output);
    }
    
    [Fact]
    public async Task ShouldCancelTimerOnExternalEvent()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt => opt.RegisterWorkflow<CancellableTimerWorkflow>(),
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
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(CancellableTimerWorkflow), workflowInstanceId);
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Cancel the timer by raising an event
        await daprWorkflowClient.RaiseEventAsync(workflowInstanceId, "CancelTimer");

        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        var duration = DateTime.UtcNow - startTime;

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<string>();
        Assert.Equal("Cancelled", output);
        
        // Verify it completed quickly (not after full timer)
        Assert.True(duration.TotalSeconds < 10, "Timer should have been cancelled early");
    }

    private sealed class CancellableTimerWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            var timerTask = context.CreateTimer(TimeSpan.FromMinutes(5));
            var eventTask = context.WaitForExternalEventAsync<object>("CancelTimer");

            var completedTask = await Task.WhenAny(timerTask, eventTask);

            if (completedTask == eventTask)
            {
                return "Cancelled";
            }

            return "Completed";
        }
    }
    
    private sealed class RetryWorkflow : Workflow<int, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, int attemptNumber)
        {
            try
            {
                await context.CallActivityAsync<string>(nameof(FlakyActivity), attemptNumber);
                return "Success after retries";
            }
            catch (Exception) when (attemptNumber < 2)
            {
                // Retry by continuing the workflow with incremented attempt
                context.ContinueAsNew(attemptNumber + 1);
                return string.Empty; // Won't be reached
            }
        }
    }

    private sealed class FlakyActivity : WorkflowActivity<int, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, int attemptCount)
        {
            if (attemptCount < 2)
                throw new InvalidOperationException("Simulated failure");
            
            return Task.FromResult("Success");
        }
    }
}
