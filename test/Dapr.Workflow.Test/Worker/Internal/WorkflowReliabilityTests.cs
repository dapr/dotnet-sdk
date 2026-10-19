// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0.

using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Client;
using Dapr.Workflow.Payloads;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker.Internal;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Workflow.Test.Worker.Internal;

public sealed class WorkflowReliabilityTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static WorkflowOrchestrationContext Context(bool payloads = false) => new(
        "workflow", "instance", Start, new JsonDaprSerializer(), NullLoggerFactory.Instance,
        new WorkflowVersionTracker([]), payloads: payloads
            ? new WorkflowPayloadHandler(new JsonDaprSerializer(), new DelayedPayloadConverter())
            : null);

    [Fact]
    public Task LegacyOptionalTimers_DoNotCancelWaitersOrResendEvents() => Task.Run(async () =>
    {
        var context = Context();
        var first = context.WaitForExternalEventAsync<string>("first", Timeout.InfiniteTimeSpan);
        var second = context.WaitForExternalEventAsync<string>("second", Timeout.InfiniteTimeSpan);
        context.SendEvent("recipient", "payment", 42);
        var activity = context.CallActivityAsync<string>("activity");
        context.ProcessEvents(
        [
            new HistoryEvent { EventId = 0, EventSent = new() { InstanceId = "recipient", Name = "payment" } },
            new HistoryEvent { EventId = 1, TaskScheduled = new() { Name = "activity", TaskExecutionId = "persisted" } }
        ], true);
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Empty(context.PendingActions);
        context.ProcessEvents(
        [
            new HistoryEvent { EventRaised = new() { Name = "first", Input = "\"a\"" } },
            new HistoryEvent { EventRaised = new() { Name = "second", Input = "\"b\"" } },
            new HistoryEvent { TaskCompleted = new() { TaskScheduledId = 99, TaskExecutionId = "persisted", Result = "\"done\"" } }
        ], false);
        Assert.Equal("a", await first.WaitAsync(TestContext.Current.CancellationToken));
        Assert.Equal("b", await second.WaitAsync(TestContext.Current.CancellationToken));
        Assert.Equal("done", await activity.WaitAsync(TestContext.Current.CancellationToken));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task LegacyOptionalTimerRemoval_PreservesConcurrentEventWait() => Task.Run(async () =>
    {
        var context = Context();
        var waiting = context.WaitForExternalEventAsync<string>("approval", Timeout.InfiniteTimeSpan);
        _ = context.CallActivityAsync<string>("activity");
        context.ProcessEvents(
        [
            new HistoryEvent { EventId = 0, TaskScheduled = new() { Name = "activity" } },
            new HistoryEvent { EventRaised = new() { Name = "approval", Input = "\"approved\"" } }
        ], true);
        Assert.Equal("approved", await waiting.WaitAsync(TestContext.Current.CancellationToken));
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public Task ContinueAsNew_DoesNotDeliverLateEventsToAbandonedWaiters(bool preserve, bool payloads) => Task.Run(async () =>
    {
        var context = Context(payloads);
        Task<string>? abandoned = null;
        async Task Run()
        {
            var first = context.WaitForExternalEventAsync<string>("a", TestContext.Current.CancellationToken);
            abandoned = context.WaitForExternalEventAsync<string>("b", TestContext.Current.CancellationToken);
            var winner = await Task.WhenAny(first, abandoned);
            context.ContinueAsNew(await winner, preserve);
        }
        var run = Run();
        await context.ProcessEventsAsync(
            [new HistoryEvent { EventRaised = new() { Name = "a", Input = "\"first\"" } }], false);
        Assert.True(run.IsCompletedSuccessfully);
        await run;
        await context.ProcessEventsAsync(
            [new HistoryEvent { EventRaised = new() { Name = "b", Input = "\"second\"" } }], false);
        context.FinalizeCarryoverEvents();
        context.FinalizeCarryoverEvents();
        Assert.False(abandoned!.IsCompleted);
        var carried = Assert.Single(context.PendingActions).CompleteWorkflow.CarryoverEvents;
        if (preserve)
            Assert.Equal("b", Assert.Single(carried).EventRaised.Name);
        else
            Assert.Empty(carried);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Suspension_BuffersAcrossTurnsAndReleasesInOrderOnResume(bool payloads) => Task.Run(async () =>
    {
        var context = Context(payloads);
        var observations = new List<(string Value, bool Replay, DateTime Time)>();
        async Task Run()
        {
            observations.Add((await context.CallActivityAsync<string>("activity"), context.IsReplaying, context.CurrentUtcDateTime));
            observations.Add((await context.WaitForExternalEventAsync<string>("signal", TestContext.Current.CancellationToken),
                context.IsReplaying, context.CurrentUtcDateTime));
        }
        var run = Run();
        context.ProcessEvents(
        [
            new HistoryEvent { EventId = 0, TaskScheduled = new() { Name = "activity" } },
            new HistoryEvent { ExecutionSuspended = new() },
            new HistoryEvent { WorkflowStarted = new(), Timestamp = Timestamp.FromDateTime(Start.AddSeconds(1)) },
            new HistoryEvent { TaskCompleted = new() { TaskScheduledId = 0, Result = "\"done\"" } }
        ], true);
        Assert.Empty(observations);
        Assert.True(context.IsSuspended);
        await context.ProcessEventsAsync(
        [
            new HistoryEvent { EventRaised = new() { Name = "signal", Input = "\"received\"" } },
            new HistoryEvent { ExecutionResumed = new() }
        ], false);
        await run.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(context.IsSuspended);
        Assert.Equal([("done", true, Start.AddSeconds(1)), ("received", false, Start.AddSeconds(1))], observations);
        context.ProcessEvents([new HistoryEvent { ExecutionSuspended = new() }], false);
        Assert.True(context.IsSuspended);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(null)]
    [InlineData("explicit-child")]
    public Task ChildRetryMetadata_ReferencesActualFirstChild_WithoutChangingGeneratedIds(string? instanceId) => Task.Run(() =>
    {
        var context = Context();
        var baseline = Context();
        _ = baseline.NewGuid(); // Historical retry-chain reservation must remain replay-compatible.
        var expectedFirst = instanceId ?? baseline.NewGuid().ToString();
        _ = context.CallChildWorkflowAsync<string>("child", options: new ChildWorkflowTaskOptions
        {
            InstanceId = instanceId,
            RetryPolicy = new WorkflowRetryPolicy(3, TimeSpan.FromSeconds(1))
        });
        Assert.Equal(expectedFirst, Assert.Single(context.PendingActions).CreateChildWorkflow.InstanceId);
        context.ProcessEvents(
        [
            new HistoryEvent { EventId = 0, ChildWorkflowInstanceCreated = new() { Name = "child", InstanceId = expectedFirst } },
            new HistoryEvent { ChildWorkflowInstanceFailed = new() { TaskScheduledId = 0, FailureDetails = new() { ErrorType = "Error" } } }
        ], true);
        Assert.Equal(expectedFirst, Assert.Single(context.PendingActions).CreateTimer.ChildWorkflowRetry.InstanceId);
        context.ProcessEvents(
        [
            new HistoryEvent { EventId = 1, TimerCreated = new() { FireAt = Timestamp.FromDateTime(Start.AddSeconds(1)) } },
            new HistoryEvent { TimerFired = new() { TimerId = 1 } }
        ], false);
        var nextChild = Assert.Single(context.PendingActions);
        Assert.Equal(instanceId ?? baseline.NewGuid().ToString(), nextChild.CreateChildWorkflow.InstanceId);
        context.ProcessEvents(
        [
            new HistoryEvent { EventId = 2, ChildWorkflowInstanceCreated = new() { InstanceId = nextChild.CreateChildWorkflow.InstanceId } },
            new HistoryEvent { ChildWorkflowInstanceFailed = new() { TaskScheduledId = 2, FailureDetails = new() { ErrorType = "Error" } } }
        ], false);
        Assert.Equal(expectedFirst, Assert.Single(context.PendingActions).CreateTimer.ChildWorkflowRetry.InstanceId);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RetryTimeout_IsCheckedAfterTimer_AndDelayIsBounded(bool child) => Task.Run(() =>
    {
        var context = Context();
        var policy = new WorkflowRetryPolicy(3, TimeSpan.FromSeconds(1), retryTimeout: TimeSpan.FromSeconds(2));
        var result = child
            ? context.CallChildWorkflowAsync<string>("child", options: new ChildWorkflowTaskOptions { InstanceId = "child", RetryPolicy = policy })
            : context.CallActivityAsync<string>("activity", options: new WorkflowTaskOptions(RetryPolicy: policy));
        var created = child
            ? new HistoryEvent { EventId = 0, ChildWorkflowInstanceCreated = new() { InstanceId = "child" } }
            : new HistoryEvent { EventId = 0, TaskScheduled = new() { Name = "activity" } };
        var failure = child
            ? new HistoryEvent { ChildWorkflowInstanceFailed = new() { TaskScheduledId = 0, FailureDetails = new() { ErrorType = "Error" } } }
            : new HistoryEvent { TaskFailed = new() { TaskScheduledId = 0, FailureDetails = new() { ErrorType = "Error" } } };
        context.ProcessEvents(
        [
            created,
            new HistoryEvent { WorkflowStarted = new(), Timestamp = Timestamp.FromDateTime(Start.AddSeconds(1.5)) },
            failure
        ], false);
        Assert.Equal(Start.AddSeconds(2), Assert.Single(context.PendingActions).CreateTimer.FireAt.ToDateTime());
        context.ProcessEvents(
        [
            new HistoryEvent { EventId = 1, TimerCreated = new() { FireAt = Timestamp.FromDateTime(Start.AddSeconds(2)) } },
            new HistoryEvent { WorkflowStarted = new(), Timestamp = Timestamp.FromDateTime(Start.AddSeconds(3)) },
            new HistoryEvent { TimerFired = new() { TimerId = 1 } }
        ], false);
        Assert.Empty(context.PendingActions);
        Assert.True(result.IsFaulted);
        Assert.IsType<WorkflowTaskFailedException>(result.Exception!.InnerException);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(2)]
    [InlineData(double.MaxValue)]
    public async Task CappedBackoff_DoesNotOverflowBeforeCap(double coefficient)
    {
        var attempts = 0;
        var retry = new RetryInterceptor<string>(Context(),
            new WorkflowRetryPolicy(100, TimeSpan.FromSeconds(1), coefficient, TimeSpan.FromSeconds(1)),
            () =>
            {
                attempts++;
                return Task.FromException<string>(new WorkflowTaskFailedException("retry",
                    new WorkflowTaskFailureDetails("Error", "retry", null)));
            },
            delay =>
            {
                Assert.Equal(TimeSpan.FromSeconds(1), delay);
                return Task.CompletedTask;
            });
        await Assert.ThrowsAsync<WorkflowTaskFailedException>(() => retry.Invoke());
        Assert.Equal(100, attempts);
    }

    private sealed class DelayedPayloadConverter : IWorkflowPayloadConverter
    {
        public ValueTask<string> EncodeAsync(string serialized, PayloadSite site, CancellationToken cancellationToken = default)
            => new(serialized);

        public async ValueTask<string?> DecodeAsync(string? wire, PayloadSite site, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return wire;
        }
    }
}
