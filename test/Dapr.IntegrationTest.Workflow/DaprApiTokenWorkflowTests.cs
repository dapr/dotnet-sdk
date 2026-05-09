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

public sealed class DaprApiTokenWorkflowTests
{
    private const string DaprApiTokenEnvVarName = "DAPR_API_TOKEN";

    [Dapr.Testcontainers.Xunit.Attributes.MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldRunWorkflowWithDaprApiTokenFromEnvVar()
    {
        const string daprApiToken = "workflow-env-token";
        var originalToken = Environment.GetEnvironmentVariable(DaprApiTokenEnvVarName);

        Environment.SetEnvironmentVariable(DaprApiTokenEnvVarName, daprApiToken);

        try
        {
            var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
            var workflowInstanceId = Guid.NewGuid().ToString();

            var options = new DaprRuntimeOptions().WithDaprApiToken(daprApiToken);

            await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(options, needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
            await environment.StartAsync(TestContext.Current.CancellationToken);

            var harness = new DaprHarnessBuilder(componentsDir)
                .WithEnvironment(environment)
                .WithOptions(options)
                .BuildWorkflow();
            await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(builder =>
                {
                    builder.Services.AddDaprWorkflowBuilder(
                        configureRuntime: opt =>
                        {
                            opt.RegisterWorkflow<TestWorkflow>();
                            opt.RegisterActivity<DoublingActivity>();
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

            const int startingValue = 8;

            await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TestWorkflow), workflowInstanceId, startingValue);
            var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId, true, TestContext.Current.CancellationToken);

            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            var resultValue = result.ReadOutputAs<int>();
            Assert.Equal(16, resultValue);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DaprApiTokenEnvVarName, originalToken);
        }
    }

    private sealed class DoublingActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            var square = input * 2;
            return Task.FromResult(square);
        }
    }

    private sealed class TestWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var result = await context.CallActivityAsync<int>(nameof(DoublingActivity), input);
            return result;
        }
    }
}
