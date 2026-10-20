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
// ------------------------------------------------------------------------

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning;

public sealed class GeneratedContributionsIntegrationTests
{
    [MinimumDaprRuntimeFact("1.17")]
    public async Task GeneratedAssembliesExecuteGlobalLatestQualifiedReplayNamesAndExplicitAliases()
    {
        var components = TestDirectoryManager.CreateTestDirectory("generated-contributions");
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();
        var harness = new DaprHarnessBuilder(components)
            .WithEnvironment(environment)
            .WithOptions(new DaprRuntimeOptions().WithAppId($"generated-contributions-{Guid.NewGuid():N}"))
            .BuildWorkflow();
        await using var app = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: options => options.RegisterWorkflow<SharedProcess>("ExplicitLatest"),
                    configureClient: (sp, client) => client.UseGrpcEndpoint(
                        sp.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"]!));
                builder.Services.AddDaprWorkflowVersioning();
            }).BuildAndStartAsync();
        using var scope = app.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        var router = scope.ServiceProvider.GetRequiredService<IWorkflowRouterRegistry>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        while (true)
        {
            try
            {
                await client.GetWorkflowStateAsync("warmup", cancellation: timeout.Token);
                break;
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
            {
                await Task.Delay(200, timeout.Token);
            }
        }

        var cases = new[]
        {
            ("SharedAcrossAssemblies", "latest:input"),
            ("global::Dapr.IntegrationTest.Workflow.Versioning.SharedProcess", "latest:input"),
            ("global::Dapr.IntegrationTest.Workflow.Versioning.ReferenceWorkflows.SharedProcess", "reference:input"),
            ("ReferenceOnlyFamily", "other:input"),
            ("ReferenceOnlyV1", "other:input"),
            ("ExplicitLatest", "latest:input")
        };
        foreach (var (name, expected) in cases)
        {
            Assert.True(router.Contains(name), name);
            var id = await client.ScheduleNewWorkflowAsync(name, instanceId: null, input: "input",
                startTime: null, cancellation: timeout.Token);
            var state = await client.WaitForWorkflowCompletionAsync(id, cancellation: timeout.Token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, state.RuntimeStatus);
            Assert.Equal(expected, state.ReadOutputAs<string>());
        }
    }
}

[WorkflowVersion(CanonicalName = "SharedAcrossAssemblies", Version = "2")]
internal sealed class SharedProcess : Workflow<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input) =>
        Task.FromResult($"latest:{input}");
}
