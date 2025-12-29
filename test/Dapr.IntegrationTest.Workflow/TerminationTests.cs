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

public sealed class TerminationTests
{
    [Fact]
    public async Task ShouldTerminateRunningWorkflow()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt => opt.RegisterWorkflow<LongRunningWorkflow>(),
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(LongRunningWorkflow), workflowInstanceId);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await daprWorkflowClient.TerminateWorkflowAsync(workflowInstanceId, "User requested termination");
        await Task.Delay(TimeSpan.FromSeconds(2));

        var state = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId);
        Assert.NotNull(state);
        Assert.Equal(WorkflowRuntimeStatus.Terminated, state.RuntimeStatus);
    }

    private sealed class LongRunningWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            await context.CreateTimer(TimeSpan.FromMinutes(10));
            return "Completed";
        }
    }
}
