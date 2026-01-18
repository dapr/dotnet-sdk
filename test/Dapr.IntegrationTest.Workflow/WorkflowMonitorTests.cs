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

public sealed class WorkflowMonitorTests
{
    [Fact]
    public async Task ShouldHandleContinueAsNew()
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
                        opt.RegisterWorkflow<DemoWorkflow>();
                        opt.RegisterActivity<CheckStatusActivity>();
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
        
        var invocationTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(DemoWorkflow), workflowInstanceId, new HealthRecord(true));
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId, cancellation: invocationTimeout.Token);
        Assert.False(invocationTimeout.IsCancellationRequested);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var resultValue = result.ReadOutputAs<int>();
        Assert.Equal(3, resultValue);
    }
    
    private sealed class DemoWorkflow : Workflow<HealthRecord, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, HealthRecord input)
        {
            var status = await context.CallActivityAsync<string>(nameof(CheckStatusActivity), true);
            int nextSleepInterval;
            if (!context.IsReplaying)
            {
                Console.WriteLine($"This job is {status}");
            }

            if (status == "healthy")
            {
                input.IsHealthy = true;
                nextSleepInterval = 30;
            }
            else
            {
                if (input.IsHealthy)
                {
                    input.IsHealthy = false;
                }
                Console.WriteLine("Status is unhealthy. Set check interval to 5s");
                nextSleepInterval = 5;
            }
            
            // If this is the third such interval, call it quits
            if (input.CurrentInterval >= 3)
                return input.CurrentInterval;
            
            await context.CreateTimer(TimeSpan.FromSeconds(nextSleepInterval));
            input = input with {CurrentInterval = input.CurrentInterval + 1};
            context.ContinueAsNew(input);
            return input.CurrentInterval;
        }
    }

    private record struct HealthRecord(bool IsHealthy, int CurrentInterval = 0); 
    
    private sealed class CheckStatusActivity : WorkflowActivity<bool, string>
    {
        private static List<string> _status = ["healthy", "unhealthy"];
        private readonly Random random = new();
        
        public override Task<string> RunAsync(WorkflowActivityContext context, bool input) => Task.FromResult(_status[random.Next(_status.Count)]);
    }
}
