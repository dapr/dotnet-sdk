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
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

/// <summary>
/// Proves that <see cref="WorkflowContext.CurrentUtcDateTime"/> is deterministic and
/// monotonically non-decreasing across workflow replays.
///
/// <para>
/// https://github.com/dapr/dotnet-sdk/issues/1764
/// Root cause of the bug: on every replay the Dapr worker initialised
/// <c>_currentUtcDateTime</c> with the <em>current turn's</em>
/// <c>OrchestratorStarted</c> timestamp before running any workflow code.
/// Because the workflow code runs from the top on each replay, any read of
/// <c>CurrentUtcDateTime</c> before the first <c>await</c> would return the latest
/// turn's timestamp instead of the initial turn's timestamp — making the value
/// inconsistent (and often <em>later</em> than timestamps captured after the first
/// activity completed).
/// </para>
/// </summary>
public sealed class CurrentUtcDateTimeConsistencyTests
{
    /// <summary>
    /// Schedules a workflow that calls two sequential activities and captures
    /// <c>CurrentUtcDateTime</c> at three checkpoints:
    /// <list type="number">
    ///   <item>Before the first activity (before any <c>await</c>)</item>
    ///   <item>After the first activity</item>
    ///   <item>After the second activity</item>
    /// </list>
    /// Each activity introduces a small delay so that Dapr assigns a strictly later
    /// <c>OrchestratorStarted</c> timestamp for the subsequent turn, making the
    /// timestamps observably distinct.
    ///
    /// Without the fix, checkpoint (1) returns the <em>third</em> turn's timestamp
    /// while checkpoint (2) returns the <em>second</em> turn's timestamp, so the
    /// sequence is not monotonically non-decreasing and the assertion fails.
    /// </summary>
    [MinimumDaprRuntimeFact("1.17")]
    public async Task CurrentUtcDateTime_IsMonotonicallyNonDecreasing_AcrossReplays()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt =>
                    {
                        opt.RegisterWorkflow<TimestampCaptureWorkflow>();
                        opt.RegisterActivity<DelayActivity>();
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
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TimestampCaptureWorkflow), workflowInstanceId);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(
            workflowInstanceId, true, TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);

        var output = result.ReadOutputAs<TimestampOutput>();
        Assert.NotNull(output);

        // All three timestamps must be valid UTC.
        Assert.Equal(DateTimeKind.Utc, output.BeforeFirstActivity.Kind);
        Assert.Equal(DateTimeKind.Utc, output.AfterFirstActivity.Kind);
        Assert.Equal(DateTimeKind.Utc, output.AfterSecondActivity.Kind);

        // CurrentUtcDateTime must be monotonically non-decreasing across replays.
        //
        // Without the fix, BeforeFirstActivity would show the third turn's
        // OrchestratorStarted timestamp (the most recent one when the workflow
        // code ran on the final replay), which is later than AfterFirstActivity
        // (second turn's timestamp). That would make the sequence:
        //   BeforeFirstActivity = T3 > AfterFirstActivity = T2  ← wrong
        //
        // With the fix, the workflow always starts with T1 (the initial turn's
        // timestamp) regardless of which replay is currently executing:
        //   T1 ≤ T2 ≤ T3  ✓
        Assert.True(
            output.BeforeFirstActivity <= output.AfterFirstActivity,
            $"CurrentUtcDateTime went backwards across a replay boundary: " +
            $"before-first-activity={output.BeforeFirstActivity:O} is later than " +
            $"after-first-activity={output.AfterFirstActivity:O}");

        Assert.True(
            output.AfterFirstActivity <= output.AfterSecondActivity,
            $"CurrentUtcDateTime went backwards: " +
            $"after-first-activity={output.AfterFirstActivity:O} is later than " +
            $"after-second-activity={output.AfterSecondActivity:O}");
    }

    /// <summary>
    /// The three <see cref="WorkflowContext.CurrentUtcDateTime"/> snapshots captured
    /// by <see cref="TimestampCaptureWorkflow"/>.
    /// </summary>
    private sealed class TimestampOutput
    {
        public DateTime BeforeFirstActivity { get; init; }
        public DateTime AfterFirstActivity { get; init; }
        public DateTime AfterSecondActivity { get; init; }
    }

    /// <summary>
    /// Activity that sleeps for <paramref name="input"/> milliseconds so that Dapr
    /// records a later <c>OrchestratorStarted</c> timestamp for the next turn.
    /// </summary>
    private sealed class DelayActivity : WorkflowActivity<int, string>
    {
        public override async Task<string> RunAsync(WorkflowActivityContext context, int input)
        {
            await Task.Delay(input);
            return "done";
        }
    }

    /// <summary>
    /// Calls two activities in sequence, capturing <see cref="WorkflowContext.CurrentUtcDateTime"/>
    /// before the first activity and after each activity.
    /// </summary>
    private sealed class TimestampCaptureWorkflow : Workflow<object?, TimestampOutput>
    {
        public override async Task<TimestampOutput> RunAsync(WorkflowContext context, object? input)
        {
            // Read before any await. On replay this is the value that was wrong:
            // the bug caused it to reflect the *current* turn's start time rather
            // than the *initial* turn's start time.
            var beforeFirstActivity = context.CurrentUtcDateTime;

            // 100 ms delays ensure each activity completes in a measurably later
            // Dapr turn, giving T1 < T2 < T3 with high confidence.
            await context.CallActivityAsync<string>(nameof(DelayActivity), 100);
            var afterFirstActivity = context.CurrentUtcDateTime;

            await context.CallActivityAsync<string>(nameof(DelayActivity), 100);
            var afterSecondActivity = context.CurrentUtcDateTime;

            return new TimestampOutput
            {
                BeforeFirstActivity = beforeFirstActivity,
                AfterFirstActivity = afterFirstActivity,
                AfterSecondActivity = afterSecondActivity
            };
        }
    }
}
