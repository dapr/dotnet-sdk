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
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class MaxConcurrentWorkflowsTests
{
    /// <summary>
    /// Verifies that setting <see cref="WorkflowRuntimeOptions.MaxConcurrentWorkflows"/> to 1
    /// does not deadlock the runtime and that all scheduled workflows eventually complete.
    /// </summary>
    [MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldCompleteAllWorkflowsWhenLimitIsOne()
    {
        const int workflowCount = 3;

        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceIds = Enumerable.Range(0, workflowCount)
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.MaxConcurrentWorkflows = 1;
                        opt.RegisterWorkflow<EchoWorkflow>();
                        opt.RegisterActivity<EchoActivity>();
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

        // Schedule all workflows concurrently.
        await Task.WhenAll(workflowInstanceIds.Select(id =>
            daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(EchoWorkflow), id, id)));

        // Wait for all to finish and assert each completed successfully.
        var results = await Task.WhenAll(workflowInstanceIds.Select(id =>
            daprWorkflowClient.WaitForWorkflowCompletionAsync(id, true, TestContext.Current.CancellationToken)));

        foreach (var (result, id) in results.Zip(workflowInstanceIds))
        {
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal(id, result.ReadOutputAs<string>());
        }
    }

    /// <summary>
    /// Verifies that a custom <see cref="WorkflowRuntimeOptions.MaxConcurrentWorkflows"/> value
    /// greater than 1 does not deadlock the runtime and that all scheduled workflows complete.
    /// </summary>
    [MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldCompleteAllWorkflowsWithCustomConcurrencyLimit()
    {
        const int limit = 2;
        const int workflowCount = 5;

        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceIds = Enumerable.Range(0, workflowCount)
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.MaxConcurrentWorkflows = limit;
                        opt.RegisterWorkflow<EchoWorkflow>();
                        opt.RegisterActivity<EchoActivity>();
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

        await Task.WhenAll(workflowInstanceIds.Select(id =>
            daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(EchoWorkflow), id, id)));

        var results = await Task.WhenAll(workflowInstanceIds.Select(id =>
            daprWorkflowClient.WaitForWorkflowCompletionAsync(id, true, TestContext.Current.CancellationToken)));

        foreach (var (result, id) in results.Zip(workflowInstanceIds))
        {
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal(id, result.ReadOutputAs<string>());
        }
    }

    private sealed class EchoActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input) =>
            Task.FromResult(input);
    }

    private sealed class EchoWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input) =>
            await context.CallActivityAsync<string>(nameof(EchoActivity), input);
    }
}
