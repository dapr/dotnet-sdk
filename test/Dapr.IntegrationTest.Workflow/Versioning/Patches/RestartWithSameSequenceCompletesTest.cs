using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning.Patches;

public sealed class PatchWorkflowVersioningE2ETests
{
    private const string ModeEnvVarName = "DAPR_WORKFLOW_PATCH_E2E_MODE";
    private const string WorkflowName = nameof(PatchProbeWorkflow);
    private const string SimpleWorkflowName = nameof(SimplePatchProbeWorkflow);
    private const string ExternalEventName = "go";

    [MinimumDaprRuntimeFact("1.17.0")]
    public async Task Workflow_PatchVersioning_RestartWithSamePatchSequence_Completes()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("patch-versioning");
        var instanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        await using (var appV1 = await BuildAndStartWorkflowAppAsync(harness))
        {
            using var scope = appV1.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await client.ScheduleNewWorkflowAsync(SimpleWorkflowName, instanceId, input: 5);

            // Give the worker a moment to process the first turn and record patch history.
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        // "Deploy" the same version again (same patch sequence) and resume the workflow.
        await using (var appV1b = await BuildAndStartWorkflowAppAsync(harness))
        {
            using var scope = appV1b.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await client.RaiseEventAsync(instanceId, ExternalEventName, "resume");
            var result = await client.WaitForWorkflowCompletionAsync(instanceId);

            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal("after=6", result.ReadOutputAs<string>());
        }
    }

    [MinimumDaprRuntimeFact("1.17.0")]
    public async Task Workflow_PatchVersioning_RestartWithReorderedPatches_Stalls()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("patch-versioning");
        var instanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        Environment.SetEnvironmentVariable(ModeEnvVarName, "v1");

        await using (var appV1 = await BuildAndStartWorkflowAppAsync(harness))
        {
            using var scope = appV1.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await client.ScheduleNewWorkflowAsync(WorkflowName, instanceId, input: 5);

            // Ensure the first turn executes and stamps history with patch sequence.
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        // "Deploy" a reordered version: replay will evaluate patches in a different order -> stall.
        Environment.SetEnvironmentVariable(ModeEnvVarName, "reordered");

        await using (var appReordered = await BuildAndStartWorkflowAppAsync(harness))
        {
            using var scope = appReordered.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await client.RaiseEventAsync(instanceId, ExternalEventName, "resume");
            var result = await client.WaitForWorkflowCompletionAsync(instanceId);

            Assert.Equal(WorkflowRuntimeStatus.Stalled, result.RuntimeStatus);
        }
    }

    [MinimumDaprRuntimeFact("1.17.0")]
    public async Task Workflow_PatchVersioning_RestartWithPatchRemovedFromReplayPath_Stalls()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("patch-versioning");
        var instanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        Environment.SetEnvironmentVariable(ModeEnvVarName, "v1");

        await using (var appV1 = await BuildAndStartWorkflowAppAsync(harness))
        {
            using var scope = appV1.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await client.ScheduleNewWorkflowAsync(WorkflowName, instanceId, input: 5);

            // Ensure patches are evaluated and recorded.
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        // "Deploy" a version that no longer evaluates the historical patch at all -> stall via
        // ValidateReplayConsumedHistoryPatches().
        Environment.SetEnvironmentVariable(ModeEnvVarName, "removed");

        await using (var appRemoved = await BuildAndStartWorkflowAppAsync(harness))
        {
            using var scope = appRemoved.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await client.RaiseEventAsync(instanceId, ExternalEventName, "resume");
            var result = await client.WaitForWorkflowCompletionAsync(instanceId);

            Assert.Equal(WorkflowRuntimeStatus.Stalled, result.RuntimeStatus);
        }
    }
    
    // [MinimumDaprRuntimeFact("1.17.0")]
    // public async Task Workflow_PatchVersioning_RestartWithDuplicateCountMismatch_Stalls()
    // {
    //     var componentsDir = TestDirectoryManager.CreateTestDirectory("patch-versioning");
    //     var instanceId = Guid.NewGuid().ToString();
    //
    //     await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
    //     await environment.StartAsync();
    //
    //     var harness = new DaprHarnessBuilder(componentsDir)
    //         .WithEnvironment(environment)
    //         .BuildWorkflow();
    //
    //     // v1 records duplicate patch evaluations: ["p1", "p1", "p2"]
    //     Environment.SetEnvironmentVariable(ModeEnvVarName, "v1");
    //
    //     await using (var appV1 = await BuildAndStartWorkflowAppAsync(harness))
    //     {
    //         using var scope = appV1.CreateScope();
    //         var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
    //
    //         await client.ScheduleNewWorkflowAsync(WorkflowName, instanceId, input: 5);
    //
    //         // Ensure first turn executes and records duplicate history.
    //         await Task.Delay(TimeSpan.FromSeconds(2));
    //     }
    //
    //     // "Deploy" a version that only evaluates one "p1" (missing the duplicate) -> stall.
    //     Environment.SetEnvironmentVariable(ModeEnvVarName, "single-p1");
    //
    //     await using (var appSingle = await BuildAndStartWorkflowAppAsync(harness))
    //     {
    //         using var scope = appSingle.CreateScope();
    //         var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
    //
    //         await client.RaiseEventAsync(instanceId, ExternalEventName, "resume");
    //         var result = await client.WaitForWorkflowCompletionAsync(instanceId);
    //
    //         Assert.Equal(WorkflowRuntimeStatus.Stalled, result.RuntimeStatus);
    //     }
    // }

    private static async Task<DaprTestApplication> BuildAndStartWorkflowAppAsync(BaseHarness harness)
    {
        return await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<PatchProbeWorkflow>();
                        opt.RegisterWorkflow<SimplePatchProbeWorkflow>();
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
    }

    private sealed class PatchProbeWorkflow : Workflow<int, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, int input)
        {
            var mode = Environment.GetEnvironmentVariable(ModeEnvVarName) ?? "v1";

            // Turn 1: evaluate patches, then yield so we can restart the worker/app and force replay.
            var before = input;

            if (mode == "v1")
            {
                // Demonstrates: non-replay returns true and stamps patches.
                if (context.IsPatched("p1")) before += 1;
                if (context.IsPatched("p2")) before += 0; // present to make sequence explicit
            }
            else if (mode == "reordered")
            {
                // Demonstrates: replay order mismatch -> stall.
                context.IsPatched("p2");
                context.IsPatched("p1");
            }
            else if (mode == "single-p1")
            {
                // Demonstrates: history recorded "p1" and "p2", but replay evaluates only one -> stall.
                context.IsPatched("p1");
            }
            else if (mode == "removed")
            {
                // Demonstrates: replay evaluates none of the historical patches -> stall via
                // ValidateReplayConsumedHistoryPatches().
            }
            else
            {
                // Default to a stable behavior if someone runs locally with an unexpected value.
                context.IsPatched("p1");
                context.IsPatched("p2");
            }

            await context.WaitForExternalEventAsync<string>(ExternalEventName);

            // Turn 2: if the workflow isn't stalled, it can finish deterministically.
            var after = before + 0;
            return $"mode={mode};after={after}";
        }
    }

    private sealed class SimplePatchProbeWorkflow : Workflow<int, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, int input)
        {
            var value = input;
            if (context.IsPatched("p1"))
            {
                value += 1;
            }

            await context.WaitForExternalEventAsync<string>(ExternalEventName);
            return $"after={value}";
        }
    }
}

