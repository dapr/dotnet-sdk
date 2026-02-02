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

public sealed class PurgeTests
{
    [Fact]
    public async Task ShouldPurgeCompletedWorkflowInstance()
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
                    opt => opt.RegisterWorkflow<SimpleWorkflow>(),
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(SimpleWorkflow), workflowInstanceId, "test");
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);

        // Verify workflow state exists before purge
        var stateBeforePurge = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId);
        Assert.NotNull(stateBeforePurge);

        // Purge the workflow instance
        await daprWorkflowClient.PurgeInstanceAsync(workflowInstanceId);

        // Verify workflow state no longer exists after purge
        var stateAfterPurge = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId);
        Assert.Null(stateAfterPurge);
    }

    private sealed class SimpleWorkflow : Workflow<string, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, string input)
        {
            return Task.FromResult($"Processed: {input}");
        }
    }
}
