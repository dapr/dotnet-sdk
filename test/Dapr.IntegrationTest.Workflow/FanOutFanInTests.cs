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

public sealed class FanOutFanInTests
{
    private const string CompletedMessage = "Workflow completed!";
    
    [Fact]
    public async Task ShouldFanOutAndFanIn()
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
                        opt.RegisterWorkflow<TestWorkflow>();
                        opt.RegisterActivity<NotifyActivity>();
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

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TestWorkflow), workflowInstanceId, "test input");
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);

        var resultValue = result.ReadOutputAs<string>();
        Assert.Equal(CompletedMessage, resultValue);
    }

    private sealed class TestWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input)
        {
            var tasks = new List<Task>();
            for (var a = 1; a <= 3; a++)
            {
                var task = context.CallActivityAsync(nameof(NotifyActivity), $"calling task {a}");
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            return CompletedMessage;
        }
    }
    
    private sealed class NotifyActivity: WorkflowActivity<string, object?>
    {
        public override Task<object?> RunAsync(WorkflowActivityContext context, string input) =>
            Task.FromResult<object?>(null);
    }
}
