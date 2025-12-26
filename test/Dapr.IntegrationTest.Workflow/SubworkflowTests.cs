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

public sealed class SubworkflowTests
{
    [Fact]
    public async Task ShouldHandleSubworkflow()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
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
