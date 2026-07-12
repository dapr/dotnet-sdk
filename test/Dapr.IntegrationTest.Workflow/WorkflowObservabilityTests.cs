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

using System.Collections.Concurrent;
using System.Diagnostics;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Telemetry;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class WorkflowObservabilityTests
{
    private static readonly TimeSpan TelemetryTimeout = TimeSpan.FromSeconds(30);
    private const string DownstreamActivitySourceName = "Dapr.Workflow.Observability.Downstream";
    private static readonly ActivitySource DownstreamActivitySource = new(DownstreamActivitySourceName);

    [MinimumDaprRuntimeFact("1.17")]
    public async Task SimpleActivity_ShouldConnectRuntimeAndSdkActivitySpans()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<SimpleObservedWorkflow>();
                opt.RegisterActivity<SimpleObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(SimpleObservedWorkflow), instanceId, 8);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(16, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(SimpleObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task ChildWorkflow_ShouldPreserveActivityTraceInChildExecution()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<ObservedParentWorkflow>();
                opt.RegisterWorkflow<ObservedChildWorkflow>();
                opt.RegisterActivity<ChildObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(ObservedParentWorkflow), instanceId, 5);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(15, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(ChildObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task ActivityRetry_ShouldCaptureEachAttemptInSameWorkflowTrace()
    {
        RetryObservedActivity.Reset();
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<RetryObservedWorkflow>();
                opt.RegisterActivity<RetryObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(RetryObservedWorkflow), instanceId);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(3, result.ReadOutputAs<int>());

        var traceIds = await WaitForRuntimeActivityTraceIdsAsync(run.Harness, nameof(RetryObservedActivity), expectedCount: 3);
        var activities = await WaitForSdkActivitiesAsync(run.Harness, nameof(RetryObservedActivity), expectedCount: 3);

        Assert.Equal(3, activities.Count);
        Assert.All(activities, activity => Assert.Contains(activity.TraceId, traceIds));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task FanOutFanIn_ShouldKeepParallelActivitySpansOnOneTrace()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<FanOutObservedWorkflow>();
                opt.RegisterActivity<FanOutObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(FanOutObservedWorkflow), instanceId, 3);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(12, result.ReadOutputAs<int>());

        var traceIds = await WaitForRuntimeActivityTraceIdsAsync(run.Harness, nameof(FanOutObservedActivity), expectedCount: 3);
        var activities = await WaitForSdkActivitiesAsync(run.Harness, nameof(FanOutObservedActivity), expectedCount: 3);

        Assert.Equal(3, activities.Count);
        Assert.All(activities, activity => Assert.Contains(activity.TraceId, traceIds));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task TimerAndExternalEvent_ShouldPreserveTraceUntilActivityExecution()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<TimerEventObservedWorkflow>();
                opt.RegisterActivity<TimerEventObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(TimerEventObservedWorkflow), instanceId);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await client.RaiseEventAsync(instanceId, "Approval", "approved", TestContext.Current.CancellationToken);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("approved-observed", result.ReadOutputAs<string>());

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(TimerEventObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task MultiAppActivity_ShouldPreserveTraceAcrossRemoteActivityHost()
    {
        var guid = Guid.NewGuid().ToString("N");
        var app1Id = $"workflow-observe-source-{guid}";
        var app2Id = $"workflow-observe-target-{guid}";

        var options1 = new DaprRuntimeOptions().WithAppId(app1Id);
        var options2 = new DaprRuntimeOptions().WithAppId(app2Id);

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness1 = CreateWorkflowHarness("workflow-observability-multiapp-1", environment, options1)
            .EnableTelemetryCapture();
        var harness2 = CreateWorkflowHarness("workflow-observability-multiapp-2", environment, options2)
            .EnableTelemetryCapture();

        await using var app1 = await StartAppAsync(
            harness1,
            opt => opt.RegisterWorkflow<MultiAppObservedWorkflow>());
        await using var app2 = await StartAppAsync(
            harness2,
            opt => opt.RegisterActivity<RemoteObservedActivity>());

        var client = GetWorkflowClient(app1);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(
            nameof(MultiAppObservedWorkflow),
            instanceId,
            new MultiAppObservedInput(app2Id, 9));
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(27, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(harness2, nameof(RemoteObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task ActivityFailure_ShouldMarkSdkActivitySpanAsError()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<FailingObservedWorkflow>();
                opt.RegisterActivity<FailingObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(FailingObservedWorkflow), instanceId);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Failed, result.RuntimeStatus);

        var traceId = await GetRuntimeActivityTraceIdAsync(run.Harness, nameof(FailingObservedActivity));
        var activities = await WaitForSdkActivitiesAsync(run.Harness, nameof(FailingObservedActivity), traceId, expectedCount: 1);
        var activity = Assert.Single(activities);

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task WorkflowFailureAfterActivityFailure_ShouldPreserveFailedActivityTraceAndHistory()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<WorkflowFailureAfterActivityFailureObservedWorkflow>();
                opt.RegisterActivity<WorkflowFailureObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(WorkflowFailureAfterActivityFailureObservedWorkflow), instanceId);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Failed, result.RuntimeStatus);

        var history = await client.GetInstanceHistoryAsync(instanceId, TestContext.Current.CancellationToken);
        Assert.Contains(history, e => e.EventType == WorkflowHistoryEventType.TaskFailed);

        var traceId = await GetRuntimeActivityTraceIdAsync(run.Harness, nameof(WorkflowFailureObservedActivity));
        var activity = Assert.Single(await WaitForSdkActivitiesAsync(
            run.Harness,
            nameof(WorkflowFailureObservedActivity),
            traceId,
            expectedCount: 1));

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task CaughtActivityFailureCompensation_ShouldTraceFailedAndCompensatingActivities()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<CaughtFailureCompensationObservedWorkflow>();
                opt.RegisterActivity<CompensatedFailingObservedActivity>();
                opt.RegisterActivity<CompensationObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(CaughtFailureCompensationObservedWorkflow), instanceId, 7);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("compensated-7", result.ReadOutputAs<string>());

        var failureTraceId = await GetRuntimeActivityTraceIdAsync(run.Harness, nameof(CompensatedFailingObservedActivity));
        var failedActivity = Assert.Single(await WaitForSdkActivitiesAsync(
            run.Harness,
            nameof(CompensatedFailingObservedActivity),
            failureTraceId,
            expectedCount: 1));
        Assert.Equal(ActivityStatusCode.Error, failedActivity.Status);

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(CompensationObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task ContinueAsNew_ShouldPreserveActivityTracingAfterContinuation()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<ContinueAsNewObservedWorkflow>();
                opt.RegisterActivity<ContinueAsNewObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(ContinueAsNewObservedWorkflow), instanceId, 0);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(2, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(ContinueAsNewObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task MultiAppChildWorkflow_ShouldPreserveTraceAcrossRemoteChildWorkflowHost()
    {
        var guid = Guid.NewGuid().ToString("N");
        var app1Id = $"workflow-observe-child-source-{guid}";
        var app2Id = $"workflow-observe-child-target-{guid}";

        var options1 = new DaprRuntimeOptions().WithAppId(app1Id);
        var options2 = new DaprRuntimeOptions().WithAppId(app2Id);

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness1 = CreateWorkflowHarness("workflow-observability-child-multiapp-1", environment, options1)
            .EnableTelemetryCapture();
        var harness2 = CreateWorkflowHarness("workflow-observability-child-multiapp-2", environment, options2)
            .EnableTelemetryCapture();

        await using var app1 = await StartAppAsync(
            harness1,
            opt => opt.RegisterWorkflow<MultiAppChildObservedParentWorkflow>());
        await using var app2 = await StartAppAsync(
            harness2,
            opt =>
            {
                opt.RegisterWorkflow<MultiAppChildObservedTargetWorkflow>();
                opt.RegisterActivity<MultiAppChildObservedActivity>();
            });

        var client = GetWorkflowClient(app1);
        var parentInstanceId = NewInstanceId();
        var childInstanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(
            nameof(MultiAppChildObservedParentWorkflow),
            parentInstanceId,
            new MultiAppChildObservedInput(app2Id, childInstanceId, 4));
        var result = await client.WaitForWorkflowCompletionAsync(parentInstanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(20, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(harness2, nameof(MultiAppChildObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task SubworkflowWithParentAndChildActivities_ShouldTraceBothWorkflowLevels()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<ParentChildActivitiesObservedParentWorkflow>();
                opt.RegisterWorkflow<ParentChildActivitiesObservedChildWorkflow>();
                opt.RegisterActivity<ParentLevelObservedActivity>();
                opt.RegisterActivity<ChildLevelObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(ParentChildActivitiesObservedParentWorkflow), instanceId, 5);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(36, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(ParentLevelObservedActivity));
        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(ChildLevelObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task DownstreamActivitySource_ShouldBeParentedUnderWorkflowActivitySpan()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<DownstreamObservedWorkflow>();
                opt.RegisterActivity<DownstreamObservedActivity>();
            },
            TestContext.Current.CancellationToken,
            configureTelemetry: collector => collector.CaptureActivitySource(DownstreamActivitySourceName));
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(DownstreamObservedWorkflow), instanceId, 6);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(7, result.ReadOutputAs<int>());

        var traceId = await GetRuntimeActivityTraceIdAsync(run.Harness, nameof(DownstreamObservedActivity));
        var workflowActivity = Assert.Single(await WaitForSdkActivitiesAsync(
            run.Harness,
            nameof(DownstreamObservedActivity),
            traceId,
            expectedCount: 1));

        var downstream = await GetCollector(run.Harness).WaitForActivityAsync(
            activity =>
                activity.SourceName == DownstreamActivitySourceName &&
                activity.DisplayName == "DownstreamOperation" &&
                activity.TraceId == workflowActivity.TraceId &&
                activity.ParentSpanId == workflowActivity.SpanId,
            TelemetryTimeout,
            TestContext.Current.CancellationToken);

        Assert.Equal(workflowActivity.TraceId, downstream.TraceId);
        Assert.Equal(workflowActivity.SpanId, downstream.ParentSpanId);
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task SuspendResume_ShouldPreserveTraceAfterWorkflowResumes()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<SuspendResumeObservedWorkflow>();
                opt.RegisterActivity<SuspendResumeObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(SuspendResumeObservedWorkflow), instanceId);
        await client.WaitForWorkflowStartAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        await client.SuspendWorkflowAsync(instanceId, "observability-test", TestContext.Current.CancellationToken);
        var suspended = await WaitForWorkflowStatusAsync(client, instanceId, WorkflowRuntimeStatus.Suspended);
        Assert.Equal(WorkflowRuntimeStatus.Suspended, suspended.RuntimeStatus);

        await client.ResumeWorkflowAsync(instanceId, "observability-test", TestContext.Current.CancellationToken);
        await client.RaiseEventAsync(instanceId, "Continue", 11, TestContext.Current.CancellationToken);

        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(22, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(SuspendResumeObservedActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task RerunWorkflow_ShouldCaptureActivityTracingForRerunInstance()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<RerunObservedWorkflow>();
                opt.RegisterActivity<RerunFirstObservedActivity>();
                opt.RegisterActivity<RerunSecondObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var sourceInstanceId = NewInstanceId();
        var rerunInstanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(RerunObservedWorkflow), sourceInstanceId, 3);
        var sourceResult = await client.WaitForWorkflowCompletionAsync(sourceInstanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, sourceResult.RuntimeStatus);
        Assert.Equal(8, sourceResult.ReadOutputAs<int>());

        var history = await client.GetInstanceHistoryAsync(sourceInstanceId, TestContext.Current.CancellationToken);
        var firstActivityScheduledEventId = history
            .First(e => e is { EventType: WorkflowHistoryEventType.TaskScheduled, EventId: >= 0 })
            .EventId;

        var actualRerunInstanceId = await client.RerunWorkflowFromEventAsync(
            sourceInstanceId,
            (uint)firstActivityScheduledEventId,
            new RerunWorkflowFromEventOptions { NewInstanceId = rerunInstanceId },
            TestContext.Current.CancellationToken);
        var rerunResult = await client.WaitForWorkflowCompletionAsync(actualRerunInstanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(rerunInstanceId, actualRerunInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, rerunResult.RuntimeStatus);
        Assert.Equal(8, rerunResult.ReadOutputAs<int>());

        var firstTraceIds = await WaitForRuntimeActivityTraceIdsAsync(run.Harness, nameof(RerunFirstObservedActivity), expectedCount: 2);
        var firstActivities = await WaitForSdkActivitiesAsync(run.Harness, nameof(RerunFirstObservedActivity), expectedCount: 2);
        Assert.All(firstActivities, activity => Assert.Contains(activity.TraceId, firstTraceIds));

        var secondTraceIds = await WaitForRuntimeActivityTraceIdsAsync(run.Harness, nameof(RerunSecondObservedActivity), expectedCount: 2);
        var secondActivities = await WaitForSdkActivitiesAsync(run.Harness, nameof(RerunSecondObservedActivity), expectedCount: 2);
        Assert.All(secondActivities, activity => Assert.Contains(activity.TraceId, secondTraceIds));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task HistoryPropagationOwnHistory_ShouldPreserveActivityTracingAcrossPropagatedChildWorkflow()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<OwnHistoryObservedParentWorkflow>();
                opt.RegisterWorkflow<HistoryObservedReceiverWorkflow>();
                opt.RegisterActivity<HistoryObservedParentActivity>();
                opt.RegisterActivity<HistoryObservedChildActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(OwnHistoryObservedParentWorkflow), instanceId, 4);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(14, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(HistoryObservedParentActivity));
        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(HistoryObservedChildActivity));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task HistoryPropagationLineage_ShouldPreserveActivityTracingAcrossWorkflowLineage()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<LineageObservedParentWorkflow>();
                opt.RegisterWorkflow<LineageObservedMiddleWorkflow>();
                opt.RegisterWorkflow<HistoryObservedReceiverWorkflow>();
                opt.RegisterActivity<HistoryObservedParentActivity>();
                opt.RegisterActivity<HistoryObservedMiddleActivity>();
                opt.RegisterActivity<HistoryObservedChildActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(LineageObservedParentWorkflow), instanceId, 2);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(14, result.ReadOutputAs<int>());

        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(HistoryObservedParentActivity));
        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(HistoryObservedMiddleActivity));
        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(HistoryObservedChildActivity));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task ParallelChildWorkflows_ShouldCaptureEachChildActivityTrace()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<ParallelChildObservedParentWorkflow>();
                opt.RegisterWorkflow<ParallelChildObservedChildWorkflow>();
                opt.RegisterActivity<ParallelChildObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(ParallelChildObservedParentWorkflow), instanceId, 3);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<int[]>();
        Assert.NotNull(output);
        Assert.Equal([10, 20, 30], output);

        var traceIds = await WaitForRuntimeActivityTraceIdsAsync(run.Harness, nameof(ParallelChildObservedActivity), expectedCount: 3);
        var activities = await WaitForSdkActivitiesAsync(run.Harness, nameof(ParallelChildObservedActivity), expectedCount: 3);

        Assert.Equal(3, activities.Count);
        Assert.All(activities, activity => Assert.Contains(activity.TraceId, traceIds));
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task TerminateWorkflow_ShouldPreserveTracingForWorkCompletedBeforeTermination()
    {
        var run = await StartWorkflowTestAsync(
            opt =>
            {
                opt.RegisterWorkflow<TerminateObservedWorkflow>();
                opt.RegisterActivity<TerminateObservedActivity>();
            },
            TestContext.Current.CancellationToken);
        await using var _ = run;

        var client = GetWorkflowClient(run.App);
        var instanceId = NewInstanceId();

        await client.ScheduleNewWorkflowAsync(nameof(TerminateObservedWorkflow), instanceId, 5);
        await WaitForSdkActivitiesAsync(run.Harness, nameof(TerminateObservedActivity), expectedCount: 1);

        await client.TerminateWorkflowAsync(instanceId, "terminated", TestContext.Current.CancellationToken);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Terminated, result.RuntimeStatus);

        var history = await client.GetInstanceHistoryAsync(instanceId, TestContext.Current.CancellationToken);
        Assert.Contains(history, e => e.EventType == WorkflowHistoryEventType.ExecutionTerminated);
        await AssertRuntimeAndSdkActivityTraceAsync(run.Harness, nameof(TerminateObservedActivity));
    }

    private static async Task<WorkflowObservabilityRun> StartWorkflowTestAsync(
        Action<WorkflowRuntimeOptions> configureRuntime,
        CancellationToken cancellationToken,
        Action<DaprTelemetryCollector>? configureTelemetry = null)
    {
        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var harness = CreateWorkflowHarness("workflow-observability", environment)
            .EnableTelemetryCapture();
        configureTelemetry?.Invoke(GetCollector(harness));
        var app = await StartAppAsync(harness, configureRuntime);

        return new WorkflowObservabilityRun(environment, harness, app);
    }

    private static WorkflowHarness CreateWorkflowHarness(
        string directoryPrefix,
        DaprTestEnvironment environment,
        DaprRuntimeOptions? options = null)
    {
        var builder = new DaprHarnessBuilder(TestDirectoryManager.CreateTestDirectory(directoryPrefix))
            .WithEnvironment(environment);
        if (options is not null)
        {
            builder.WithOptions(options);
        }

        return builder.BuildWorkflow();
    }

    private static Task<DaprTestApplication> StartAppAsync(
        WorkflowHarness harness,
        Action<WorkflowRuntimeOptions> configureRuntime) =>
        DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime,
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrWhiteSpace(grpcEndpoint))
                        {
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                        }
                    });
            })
            .BuildAndStartAsync();

    private static DaprWorkflowClient GetWorkflowClient(DaprTestApplication app)
    {
        var scope = app.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
    }

    private static async Task<WorkflowState> WaitForWorkflowStatusAsync(
        DaprWorkflowClient client,
        string instanceId,
        WorkflowRuntimeStatus status)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var state = await client.GetWorkflowStateAsync(instanceId, cancellation: TestContext.Current.CancellationToken);
            if (state?.RuntimeStatus == status)
            {
                return state;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Expected workflow '{instanceId}' to reach status '{status}' within 30 seconds.");
        throw new UnreachableException();
    }

    private static async Task AssertRuntimeAndSdkActivityTraceAsync(WorkflowHarness harness, string activityName)
    {
        var traceId = await GetRuntimeActivityTraceIdAsync(harness, activityName);
        var activities = await WaitForSdkActivitiesAsync(harness, activityName, traceId, expectedCount: 1);

        var activity = Assert.Single(activities);
        Assert.False(string.IsNullOrWhiteSpace(activity.ParentId));
        Assert.False(string.IsNullOrWhiteSpace(activity.SpanId));
    }

    private static async Task<string> GetRuntimeActivityTraceIdAsync(WorkflowHarness harness, string activityName)
    {
        var collector = GetCollector(harness);
        var runtimeActivitySpan = await collector.WaitForZipkinSpanAsync(
            span => string.Equals(span.Name, RuntimeActivitySpanName(activityName), StringComparison.OrdinalIgnoreCase),
            TelemetryTimeout,
            TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(runtimeActivitySpan.Id));
        Assert.False(string.IsNullOrWhiteSpace(runtimeActivitySpan.TraceId));
        return runtimeActivitySpan.TraceId;
    }

    private static async Task<IReadOnlySet<string>> WaitForRuntimeActivityTraceIdsAsync(
        WorkflowHarness harness,
        string activityName,
        int expectedCount)
    {
        var collector = GetCollector(harness);
        var runtimeName = RuntimeActivitySpanName(activityName);
        var deadline = DateTimeOffset.UtcNow + TelemetryTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var traceIds = collector.ZipkinSpans
                .Where(span => string.Equals(span.Name, runtimeName, StringComparison.OrdinalIgnoreCase))
                .Select(span => span.TraceId)
                .Where(traceId => !string.IsNullOrWhiteSpace(traceId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (traceIds.Count >= expectedCount)
            {
                return traceIds;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        Assert.Fail(
            $"Expected {expectedCount} runtime activity trace(s) named '{runtimeName}', " +
            $"but captured {collector.ZipkinSpans.Count} Zipkin span(s).");
        throw new UnreachableException();
    }

    private static async Task<IReadOnlyList<CapturedActivity>> WaitForSdkActivitiesAsync(
        WorkflowHarness harness,
        string activityName,
        string traceId,
        int expectedCount)
    {
        var collector = GetCollector(harness);
        var displayName = $"WorkflowActivity {activityName}";
        var deadline = DateTimeOffset.UtcNow + TelemetryTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var matches = collector.Activities
                .Where(activity =>
                    activity.SourceName == "Dapr.Workflow" &&
                    activity.DisplayName == displayName &&
                    string.Equals(activity.TraceId, traceId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count >= expectedCount)
            {
                return matches;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        Assert.Fail(
            $"Expected {expectedCount} SDK activity span(s) named '{displayName}' on trace '{traceId}', " +
            $"but captured {collector.Activities.Count} activity span(s).");
        throw new UnreachableException();
    }

    private static async Task<IReadOnlyList<CapturedActivity>> WaitForSdkActivitiesAsync(
        WorkflowHarness harness,
        string activityName,
        int expectedCount)
    {
        var collector = GetCollector(harness);
        var displayName = $"WorkflowActivity {activityName}";
        var deadline = DateTimeOffset.UtcNow + TelemetryTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var matches = collector.Activities
                .Where(activity =>
                    activity.SourceName == "Dapr.Workflow" &&
                    activity.DisplayName == displayName)
                .ToList();

            if (matches.Count >= expectedCount)
            {
                return matches;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        Assert.Fail(
            $"Expected {expectedCount} SDK activity span(s) named '{displayName}', " +
            $"but captured {collector.Activities.Count} activity span(s).");
        throw new UnreachableException();
    }

    private static DaprTelemetryCollector GetCollector(WorkflowHarness harness) =>
        harness.TelemetryCollector ?? throw new InvalidOperationException("Telemetry capture was not enabled.");

    private static string RuntimeActivitySpanName(string activityName) =>
        $"activity||{activityName.ToLowerInvariant()}";

    private static string NewInstanceId() => $"observability-{Guid.NewGuid():N}";

    private sealed class WorkflowObservabilityRun(
        DaprTestEnvironment environment,
        WorkflowHarness harness,
        DaprTestApplication app) : IAsyncDisposable
    {
        public WorkflowHarness Harness => harness;

        public DaprTestApplication App => app;

        public async ValueTask DisposeAsync()
        {
            await app.DisposeAsync();
            await environment.DisposeAsync();
        }
    }

    private sealed class SimpleObservedWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) =>
            context.CallActivityAsync<int>(nameof(SimpleObservedActivity), input);
    }

    private sealed class SimpleObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 2);
    }

    private sealed class ObservedParentWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) =>
            context.CallChildWorkflowAsync<int>(nameof(ObservedChildWorkflow), input);
    }

    private sealed class ObservedChildWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) =>
            context.CallActivityAsync<int>(nameof(ChildObservedActivity), input);
    }

    private sealed class ChildObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 3);
    }

    private sealed class RetryObservedWorkflow : Workflow<object?, int>
    {
        private static readonly WorkflowTaskOptions RetryOptions = new()
        {
            RetryPolicy = new WorkflowRetryPolicy(
                maxNumberOfAttempts: 3,
                firstRetryInterval: TimeSpan.FromSeconds(1),
                backoffCoefficient: 1),
        };

        public override Task<int> RunAsync(WorkflowContext context, object? input) =>
            context.CallActivityAsync<int>(nameof(RetryObservedActivity), string.Empty, RetryOptions);
    }

    private sealed class RetryObservedActivity : WorkflowActivity<string?, int>
    {
        private static readonly ConcurrentDictionary<string, int> Attempts = new(StringComparer.Ordinal);

        public static void Reset() => Attempts.Clear();

        public override Task<int> RunAsync(WorkflowActivityContext context, string? input)
        {
            var attempt = Attempts.AddOrUpdate(context.InstanceId, _ => 1, (_, current) => current + 1);
            return attempt < 3
                ? throw new InvalidOperationException("Observed retry")
                : Task.FromResult(attempt);
        }
    }

    private sealed class FanOutObservedWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var tasks = Enumerable.Range(1, input)
                .Select(value => context.CallActivityAsync<int>(nameof(FanOutObservedActivity), value))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            return results.Sum();
        }
    }

    private sealed class FanOutObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 2);
    }

    private sealed class TimerEventObservedWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            await context.CreateTimer(TimeSpan.FromSeconds(1));
            var approval = await context.WaitForExternalEventAsync<string>("Approval");
            return await context.CallActivityAsync<string>(nameof(TimerEventObservedActivity), approval);
        }
    }

    private sealed class TimerEventObservedActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input) =>
            Task.FromResult($"{input}-observed");
    }

    private sealed record MultiAppObservedInput(string TargetAppId, int Value);

    private sealed class MultiAppObservedWorkflow : Workflow<MultiAppObservedInput, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, MultiAppObservedInput input) =>
            context.CallActivityAsync<int>(
                nameof(RemoteObservedActivity),
                input.Value,
                new WorkflowTaskOptions(TargetAppId: input.TargetAppId));
    }

    private sealed class RemoteObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 3);
    }

    private sealed class FailingObservedWorkflow : Workflow<string?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string? input)
        {
            await context.CallActivityAsync<string>(nameof(FailingObservedActivity), string.Empty);
            return "unreachable";
        }
    }

    private sealed class FailingObservedActivity : WorkflowActivity<string?, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string? input) =>
            throw new InvalidOperationException("Observed failure");
    }

    private sealed class WorkflowFailureAfterActivityFailureObservedWorkflow : Workflow<string?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string? input)
        {
            await context.CallActivityAsync<string>(nameof(WorkflowFailureObservedActivity), string.Empty);
            return "unreachable";
        }
    }

    private sealed class WorkflowFailureObservedActivity : WorkflowActivity<string?, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string? input) =>
            throw new InvalidOperationException("Observed workflow failure");
    }

    private sealed class CaughtFailureCompensationObservedWorkflow : Workflow<int, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, int input)
        {
            try
            {
                await context.CallActivityAsync<string>(nameof(CompensatedFailingObservedActivity), input);
                return "unreachable";
            }
            catch (WorkflowTaskFailedException)
            {
                return await context.CallActivityAsync<string>(nameof(CompensationObservedActivity), input);
            }
        }
    }

    private sealed class CompensatedFailingObservedActivity : WorkflowActivity<int, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, int input) =>
            throw new InvalidOperationException("Observed compensated failure");
    }

    private sealed class CompensationObservedActivity : WorkflowActivity<int, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult($"compensated-{input}");
    }

    private sealed class ContinueAsNewObservedWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            if (input == 0)
            {
                context.ContinueAsNew(1);
                return -1;
            }

            return await context.CallActivityAsync<int>(nameof(ContinueAsNewObservedActivity), input);
        }
    }

    private sealed class ContinueAsNewObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input + 1);
    }

    private sealed record MultiAppChildObservedInput(string TargetAppId, string ChildInstanceId, int Value);

    private sealed class MultiAppChildObservedParentWorkflow : Workflow<MultiAppChildObservedInput, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, MultiAppChildObservedInput input) =>
            context.CallChildWorkflowAsync<int>(
                nameof(MultiAppChildObservedTargetWorkflow),
                input.Value,
                new ChildWorkflowTaskOptions
                {
                    InstanceId = input.ChildInstanceId,
                    TargetAppId = input.TargetAppId
                });
    }

    private sealed class MultiAppChildObservedTargetWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) =>
            context.CallActivityAsync<int>(nameof(MultiAppChildObservedActivity), input);
    }

    private sealed class MultiAppChildObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 5);
    }

    private sealed class ParentChildActivitiesObservedParentWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var parentValue = await context.CallActivityAsync<int>(nameof(ParentLevelObservedActivity), input);
            var childValue = await context.CallChildWorkflowAsync<int>(
                nameof(ParentChildActivitiesObservedChildWorkflow),
                parentValue,
                new ChildWorkflowTaskOptions(InstanceId: $"{context.InstanceId}-child"));

            return parentValue + childValue;
        }
    }

    private sealed class ParentChildActivitiesObservedChildWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) =>
            context.CallActivityAsync<int>(nameof(ChildLevelObservedActivity), input);
    }

    private sealed class ParentLevelObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input + 7);
    }

    private sealed class ChildLevelObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 2);
    }

    private sealed class DownstreamObservedWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) =>
            context.CallActivityAsync<int>(nameof(DownstreamObservedActivity), input);
    }

    private sealed class DownstreamObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            using var activity = DownstreamActivitySource.StartActivity("DownstreamOperation");
            activity?.SetTag("workflow.instance_id", context.InstanceId);
            return Task.FromResult(input + 1);
        }
    }

    private sealed class SuspendResumeObservedWorkflow : Workflow<object?, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, object? input)
        {
            var value = await context.WaitForExternalEventAsync<int>("Continue");
            return await context.CallActivityAsync<int>(nameof(SuspendResumeObservedActivity), value);
        }
    }

    private sealed class SuspendResumeObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 2);
    }

    private sealed class RerunObservedWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var first = await context.CallActivityAsync<int>(nameof(RerunFirstObservedActivity), input);
            return await context.CallActivityAsync<int>(nameof(RerunSecondObservedActivity), first);
        }
    }

    private sealed class RerunFirstObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input + 1);
    }

    private sealed class RerunSecondObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 2);
    }

    private sealed class OwnHistoryObservedParentWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var parentValue = await context.CallActivityAsync<int>(nameof(HistoryObservedParentActivity), input);
            var childOptions = new ChildWorkflowTaskOptions(InstanceId: $"{context.InstanceId}-child")
                .WithHistoryPropagation(HistoryPropagationScope.OwnHistory);

            return await context.CallChildWorkflowAsync<int>(
                nameof(HistoryObservedReceiverWorkflow),
                parentValue,
                childOptions);
        }
    }

    private sealed class LineageObservedParentWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var parentValue = await context.CallActivityAsync<int>(nameof(HistoryObservedParentActivity), input);
            var childOptions = new ChildWorkflowTaskOptions(InstanceId: $"{context.InstanceId}-middle")
                .WithHistoryPropagation(HistoryPropagationScope.Lineage);

            return await context.CallChildWorkflowAsync<int>(
                nameof(LineageObservedMiddleWorkflow),
                parentValue,
                childOptions);
        }
    }

    private sealed class LineageObservedMiddleWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var middleValue = await context.CallActivityAsync<int>(nameof(HistoryObservedMiddleActivity), input);
            var childOptions = new ChildWorkflowTaskOptions(InstanceId: $"{context.InstanceId}-leaf")
                .WithHistoryPropagation(HistoryPropagationScope.Lineage);

            return await context.CallChildWorkflowAsync<int>(
                nameof(HistoryObservedReceiverWorkflow),
                middleValue,
                childOptions);
        }
    }

    private sealed class HistoryObservedReceiverWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            _ = context.GetPropagatedHistory();
            await context.CreateTimer(TimeSpan.Zero);
            return await context.CallActivityAsync<int>(nameof(HistoryObservedChildActivity), input);
        }
    }

    private sealed class HistoryObservedParentActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input + 3);
    }

    private sealed class HistoryObservedMiddleActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input + 2);
    }

    private sealed class HistoryObservedChildActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input * 2);
    }

    private sealed class ParallelChildObservedParentWorkflow : Workflow<int, int[]>
    {
        public override async Task<int[]> RunAsync(WorkflowContext context, int input)
        {
            var tasks = Enumerable.Range(1, input)
                .Select(value => context.CallChildWorkflowAsync<int>(
                    nameof(ParallelChildObservedChildWorkflow),
                    value * 10,
                    new ChildWorkflowTaskOptions(InstanceId: $"{context.InstanceId}-child-{value}")))
                .ToArray();

            return await Task.WhenAll(tasks);
        }
    }

    private sealed class ParallelChildObservedChildWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) =>
            context.CallActivityAsync<int>(nameof(ParallelChildObservedActivity), input);
    }

    private sealed class ParallelChildObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input);
    }

    private sealed class TerminateObservedWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            await context.CallActivityAsync<int>(nameof(TerminateObservedActivity), input);
            await context.WaitForExternalEventAsync<string>("never");
            return -1;
        }
    }

    private sealed class TerminateObservedActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input) =>
            Task.FromResult(input);
    }
}
