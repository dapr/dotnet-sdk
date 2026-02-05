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

public sealed class TerminateWorkflowTests
{
    [Fact]
    public async Task ShouldTerminateRunningWorkflow()
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
                    opt => opt.RegisterWorkflow<LongRunningWorkflow>(),
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(LongRunningWorkflow), workflowInstanceId);

        var startState = await daprWorkflowClient.WaitForWorkflowStartAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Running, startState.RuntimeStatus);

        using var preTerminationCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId, cancellation: preTerminationCts.Token));

        const string terminationOutput = "terminated";
        await daprWorkflowClient.TerminateWorkflowAsync(workflowInstanceId, terminationOutput);

        using var completionCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        WorkflowState result;
        try
        {
            result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId,
                cancellation: completionCts.Token);
        }
        catch (OperationCanceledException)
        {
            var state = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId, getInputsAndOutputs: true);
            Assert.Fail($"Timed out waiting for workflow termination. Current state: {state?.RuntimeStatus}, CustomStatus: {state?.ReadCustomStatusAs<string>()}");
            throw;
        }

        Assert.Equal(WorkflowRuntimeStatus.Terminated, result.RuntimeStatus);
        var output = result.ReadOutputAs<string>();
        Assert.Equal(terminationOutput, output);
    }

    [Fact]
    public async Task ShouldReturnFromTerminateGrpcCall()
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
                    opt => opt.RegisterWorkflow<LongRunningWorkflow>(),
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(LongRunningWorkflow), workflowInstanceId);
        await daprWorkflowClient.WaitForWorkflowStartAsync(workflowInstanceId);

        using var terminateCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await daprWorkflowClient.TerminateWorkflowAsync(workflowInstanceId, cancellation: terminateCts.Token);
        }
        catch (OperationCanceledException)
        {
            var state = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId, getInputsAndOutputs: true);
            Assert.Fail($"Terminate gRPC call timed out. Current state: {state?.RuntimeStatus}, CustomStatus: {state?.ReadCustomStatusAs<string>()}");
            throw;
        }
    }

    [Fact]
    public async Task ShouldObserveTerminatedStatusAfterTermination()
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
                    opt => opt.RegisterWorkflow<LongRunningWorkflow>(),
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(LongRunningWorkflow), workflowInstanceId);
        await daprWorkflowClient.WaitForWorkflowStartAsync(workflowInstanceId);

        await daprWorkflowClient.TerminateWorkflowAsync(workflowInstanceId);

        using var statusCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            var state = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId, getInputsAndOutputs: true,
                cancellation: statusCts.Token);
            if (state?.RuntimeStatus == WorkflowRuntimeStatus.Terminated)
            {
                Assert.Equal(WorkflowRuntimeStatus.Terminated, state.RuntimeStatus);
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), statusCts.Token);
        }
    }

    private sealed class LongRunningWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            await context.WaitForExternalEventAsync<string>("never");
            return "Completed";
        }
    }
}
