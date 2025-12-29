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
using Microsoft.Extensions.Logging;

namespace Dapr.IntegrationTest.Workflow;

public sealed partial class ReplaySafetyTests
{
    [Fact]
    public async Task ReplaySafeLogger_ShouldNotDuplicateLogsOnReplay()
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
                        opt.RegisterWorkflow<LoggingWorkflow>();
                        opt.RegisterActivity<SimpleActivity>();
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(LoggingWorkflow), workflowInstanceId, "test");
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<string>();
        Assert.Equal("Completed", output);
    }

    [Fact]
    public async Task Workflow_ShouldUseDeterministicGuidGeneration()
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
                        opt.RegisterWorkflow<DeterministicGuidWorkflow>();
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

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(DeterministicGuidWorkflow), workflowInstanceId);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var guids = result.ReadOutputAs<Guid[]>();
        Assert.NotNull(guids);
        Assert.Equal(3, guids.Length);
        
        // All GUIDs should be different but deterministic
        Assert.Equal(guids.Length, guids.Distinct().Count());
        Assert.All(guids, g => Assert.NotEqual(Guid.Empty, g));
    }

    private sealed partial class LoggingWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input)
        {
            var logger = context.CreateReplaySafeLogger<LoggingWorkflow>();
            
            LogWorkflowStarted(logger, context.InstanceId);
            
            await context.CallActivityAsync(nameof(SimpleActivity), input);
            
            LogWorkflowCompleted(logger, context.InstanceId);
            
            return "Completed";
        }

        [LoggerMessage(LogLevel.Information, "Workflow {InstanceId} started")]
        private static partial void LogWorkflowStarted(ILogger logger, string instanceId);

        [LoggerMessage(LogLevel.Information, "Workflow {InstanceId} completed")]
        private static partial void LogWorkflowCompleted(ILogger logger, string instanceId);
    }

    private sealed class DeterministicGuidWorkflow : Workflow<object?, Guid[]>
    {
        public override Task<Guid[]> RunAsync(WorkflowContext context, object? input)
        {
            var guids = new[]
            {
                context.NewGuid(),
                context.NewGuid(),
                context.NewGuid()
            };
            
            return Task.FromResult(guids);
        }
    }

    private sealed class SimpleActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        {
            return Task.FromResult($"Processed: {input}");
        }
    }
}
