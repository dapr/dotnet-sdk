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

using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

[CollectionDefinition("multi-app-activities", DisableParallelization = true)]
public sealed class MultiAppActivityTests
{
    [Fact]
    public async Task ShouldScheduleActivityOnRemoteAppUsingAppId()
    {
        var guid = Guid.NewGuid().ToString("N");
        var app1Id = $"workflow-app-1-{guid}";
        var app2Id = $"workflow-app-2-{guid}";

        var options1 = new DaprRuntimeOptions();
        options1.WithAppId(app1Id);

        var options2 = new DaprRuntimeOptions();
        options2.WithAppId(app2Id);

        var componentsDir1 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-activity-1");
        var componentsDir2 = TestDirectoryManager.CreateTestDirectory("workflow-multiapp-activity-2");

        // Build our shared environment (Network + Control plane)
        await using var environment = new DaprTestEnvironment(needsActorState: true);
        await environment.StartAsync();

        // Build and start the first application (caller)
        var harness1 = new DaprHarnessBuilder(options1, environment).BuildWorkflow(componentsDir1);
        await using var app1 = await DaprHarnessBuilder.ForHarness(harness1)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        // App1 only registers the initiating workflow
                        opt.RegisterWorkflow<InitialWorkflow>();
                        opt.AppId = app1Id;
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            }).BuildAndStartAsync();

        // Build and start the second application (target activity host)
        var harness2 = new DaprHarnessBuilder(options2, environment).BuildWorkflow(componentsDir2);
        await using var app2 = await DaprHarnessBuilder.ForHarness(harness2)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        // App2 only needs to register the activity that will be invoked remotely
                        opt.RegisterActivity<MultiplyByThreeActivity>();
                        opt.AppId = app2Id;
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            }).BuildAndStartAsync();

        using var scope1 = app1.CreateScope();
        var client1 = scope1.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        const int inputValue = 7;
        const string workflowInstanceId = "multiapp-remote-activity-instance";

        // Start the workflow on App1
        await client1.ScheduleNewWorkflowAsync(
            nameof(InitialWorkflow),
            workflowInstanceId,
            new InitialWorkflowInput(app2Id, inputValue));

        // Wait for the workflow completion (the activity itself runs on App2)
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await client1.WaitForWorkflowCompletionAsync(workflowInstanceId, cancellation: cts.Token);
        if (cts.Token.IsCancellationRequested)
        {
            Assert.Fail("Waiting for workflow completion timed out");
        }

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<int>();
        Assert.Equal(inputValue * 3, output);
    }

    private sealed record InitialWorkflowInput(string TargetAppId, int Value);

    private sealed class InitialWorkflow : Workflow<InitialWorkflowInput, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, InitialWorkflowInput input) =>
            // Call an activity that is registered in another app (App2) via the multi-app workflow routing.
            context.CallActivityAsync<int>(
                nameof(MultiplyByThreeActivity),
                input.Value,
                new WorkflowTaskOptions(TargetAppId: input.TargetAppId));
    }

    private sealed class MultiplyByThreeActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 3);
    }
}
