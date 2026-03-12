// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Dapr.Workflow.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class WorkflowRpcTests
{
    [Fact]
    public async Task ListInstanceIds_ShouldReturnScheduledWorkflowInstances()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();
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
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        // Schedule a workflow and wait for completion
        var instanceId = Guid.NewGuid().ToString();
        await client.ScheduleNewWorkflowAsync(nameof(SimpleWorkflow), instanceId, "hello");
        await client.WaitForWorkflowCompletionAsync(instanceId);

        // List instance IDs and verify our workflow appears
        var page = await client.ListInstanceIdsAsync();

        Assert.NotNull(page);
        Assert.Contains(instanceId, page.InstanceIds);
    }

    [Fact]
    public async Task GetInstanceHistory_ShouldReturnHistoryForCompletedWorkflow()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt =>
                    {
                        opt.RegisterWorkflow<WorkflowWithActivity>();
                        opt.RegisterActivity<EchoActivity>();
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
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        // Schedule a workflow with an activity and wait for completion
        var instanceId = Guid.NewGuid().ToString();
        await client.ScheduleNewWorkflowAsync(nameof(WorkflowWithActivity), instanceId, "test-input");
        var result = await client.WaitForWorkflowCompletionAsync(instanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);

        // Get history and verify it has events
        var history = await client.GetInstanceHistoryAsync(instanceId);

        Assert.NotNull(history);
        Assert.NotEmpty(history);
        // Should contain at least an ExecutionStarted event
        Assert.Contains(history, e => e.EventType == WorkflowHistoryEventType.ExecutionStarted);
    }

    private sealed class SimpleWorkflow : Workflow<string, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, string input)
        {
            return Task.FromResult($"Processed: {input}");
        }
    }

    private sealed class WorkflowWithActivity : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input)
        {
            return await context.CallActivityAsync<string>(nameof(EchoActivity), input);
        }
    }

    private sealed class EchoActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        {
            return Task.FromResult($"Echo: {input}");
        }
    }
}
