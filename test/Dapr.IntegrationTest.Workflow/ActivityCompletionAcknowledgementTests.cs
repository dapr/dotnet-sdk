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

using System.Collections.Concurrent;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class ActivityCompletionAcknowledgementTests
{
    [Fact]
    public async Task ActivityCompletion_ShouldNotBeRetried_WhenAcknowledged()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir).BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<SingleActivityWorkflow>();
                        opt.RegisterActivity<CountingActivity>();
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

        CountingActivity.Reset(workflowInstanceId);

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(SingleActivityWorkflow), workflowInstanceId, "start");
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId, true);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var executionCount = result.ReadOutputAs<int>();

        Assert.Equal(1, executionCount);
        Assert.Equal(1, CountingActivity.GetCount(workflowInstanceId));
    }

    private sealed class CountingActivity : WorkflowActivity<string, int>
    {
        private static readonly ConcurrentDictionary<string, int> Counts = new(StringComparer.Ordinal);

        public override Task<int> RunAsync(WorkflowActivityContext context, string input)
        {
            var count = Counts.AddOrUpdate(context.InstanceId, _ => 1, (_, current) => current + 1);
            return Task.FromResult(count);
        }

        public static void Reset(string instanceId) => Counts.TryRemove(instanceId, out _);

        public static int GetCount(string instanceId) =>
            Counts.TryGetValue(instanceId, out var count) ? count : 0;
    }

    private sealed class SingleActivityWorkflow : Workflow<string, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, string input) =>
            context.CallActivityAsync<int>(nameof(CountingActivity), input);
    }
}
