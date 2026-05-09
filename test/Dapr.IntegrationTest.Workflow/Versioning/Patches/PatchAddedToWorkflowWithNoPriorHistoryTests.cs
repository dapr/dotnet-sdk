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
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning.Patches;

/// <summary>
/// Regression test: adding an <c>IsPatched</c> guard to a workflow that was originally deployed
/// without any <c>IsPatched</c> calls must not corrupt the task-sequence during replay.
///
/// Failure mode (before fix): the runtime holds no version / patch history for the in-flight
/// instance. When <c>IsPatched</c> is called during replay, <c>WorkflowOrchestrationContext</c>
/// incorrectly passes <c>isReplaying: false</c> to <c>WorkflowVersionTracker.RequestPatch</c>
/// (because <c>hasPatchHistory == false</c>). <c>RequestPatch</c> therefore returns <c>true</c>,
/// causing the new patched-path activity to consume task-slot 0 (the original email-activity
/// slot). The original email activity is then assigned slot 1, and the <c>TimerFired</c> event
/// (also on slot 1) resolves that TCS. <c>HandleHistoryMatch</c> throws
/// <c>InvalidOperationException: "Unexpected history event type for task ID 1"</c>, leaving the
/// workflow in the <c>Failed</c> state.
///
/// Fix: pass <c>isReplaying: this.IsReplaying</c> unconditionally (without <c>&amp;&amp; hasPatchHistory</c>).
/// </summary>
public sealed class PatchAddedToWorkflowWithNoPriorHistoryTests
{
    // Environment variable shared between the two "deployments" within a single test run.
    // Controlled inside the test; set before starting each app instance.
    private const string ModeEnvVar = "DAPR_PATCH_NO_HISTORY_TEST_MODE";

    /// <summary>
    /// Timer duration used in the V1 workflow.  Long enough that we can stop the app before it
    /// fires, short enough that the test completes in a reasonable time.
    /// </summary>
    private static readonly TimeSpan TimerDuration = TimeSpan.FromSeconds(10);

    [MinimumDaprRuntimeFact("1.17.0")]
    public async Task Workflow_PatchAddedAfterDeploy_NoPriorPatchHistory_CompletesSuccessfully()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("patch-no-prior-history");
        var instanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();

        // ── Phase 1: deploy V1 (no IsPatched) ────────────────────────────────────────────────
        // The workflow calls EmailActivity and then waits on a durable timer.
        // We stop the app while the timer is still in-flight so the Dapr sidecar owns the
        // timer state.  When V2 starts, the sidecar delivers the TimerFired new-event, which
        // is what triggers the bug.
        Environment.SetEnvironmentVariable(ModeEnvVar, "v1");

        await using (var appV1 = await BuildWorkflowAppAsync(harness))
        {
            using var scope = appV1.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            await client.ScheduleNewWorkflowAsync(nameof(NotifyWorkflow), instanceId, "user@example.com");

            // Wait long enough for the EmailActivity to complete and for the timer to be
            // recorded in the workflow history, but short enough that the timer has NOT yet
            // fired (timer duration is 10 s, we wait 3 s).
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        // ── Phase 2: deploy V2 (IsPatched added before EmailActivity) ────────────────────────
        // The patched path would call SmsActivity first.  For a correctly replaying workflow
        // IsPatched("sms") must return false (patch not in prior history), so only EmailActivity
        // runs, preserving task-slot 0 for the history's TaskCompleted(EmailActivity) event and
        // task-slot 1 for the TimerFired event.
        Environment.SetEnvironmentVariable(ModeEnvVar, "v2");

        await using (var appV2 = await BuildWorkflowAppAsync(harness))
        {
            using var scope = appV2.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            WorkflowState result;
            try
            {
                result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: cts.Token);
            }
            catch (OperationCanceledException)
            {
                var state = await client.GetWorkflowStateAsync(instanceId, getInputsAndOutputs: true);
                Assert.Fail(
                    $"Timed out waiting for workflow '{instanceId}' to complete. " +
                    $"Current status: {state?.RuntimeStatus}.");
                return;
            }

            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);

            // The in-flight workflow was started with V1 code (no IsPatched).  On replay,
            // IsPatched("sms") must evaluate to false, so only EmailActivity runs.
            // The expected output is "email" – not "sms+email" which would indicate the
            // patched path was incorrectly entered during replay.
            var output = result.ReadOutputAs<string>();
            Assert.Equal("email", output);
        }
    }

    private static Task<DaprTestApplication> BuildWorkflowAppAsync(BaseHarness harness)
    {
        return DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<NotifyWorkflow>();
                        opt.RegisterActivity<EmailActivity>();
                        opt.RegisterActivity<SmsActivity>();
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

    /// <summary>
    /// Workflow under test.
    ///
    /// V1 behaviour (mode = "v1"): EmailActivity → timer → return "email"
    /// V2 behaviour (mode = "v2"): if IsPatched("sms") { SmsActivity } → EmailActivity → timer → return
    ///
    /// When replaying an in-flight V1 instance under V2 code, <c>IsPatched("sms")</c> must
    /// return <c>false</c> so the code path matches the original V1 execution.
    /// </summary>
    private sealed class NotifyWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input)
        {
            var mode = Environment.GetEnvironmentVariable(ModeEnvVar) ?? "v1";
            var result = string.Empty;

            if (mode == "v2" && context.IsPatched("sms"))
            {
                result += await context.CallActivityAsync<string>(nameof(SmsActivity), input);
            }

            result += await context.CallActivityAsync<string>(nameof(EmailActivity), input);

            await context.CreateTimer(TimerDuration);

            return result;
        }
    }

    private sealed class EmailActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
            => Task.FromResult("email");
    }

    private sealed class SmsActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
            => Task.FromResult("sms+");
    }
}
