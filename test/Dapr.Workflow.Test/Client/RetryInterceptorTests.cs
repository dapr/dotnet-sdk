// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0.

using Dapr.Workflow.Client;
using Moq;

namespace Dapr.Workflow.Test.Client;

public sealed class RetryInterceptorTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, 864, DateTimeKind.Utc);

    [Theory]
    [InlineData(0)]
    [InlineData(250)]
    public async Task DeadlineWait_FiringEarly_PreservesFailureWithoutAnotherAttempt(int failureMilliseconds)
    {
        var now = Start;
        var context = new Mock<IWorkflowContext>();
        context.SetupGet(c => c.CurrentUtcDateTime).Returns(() => now);
        var failure = new WorkflowTaskFailedException("original failure",
            new WorkflowTaskFailureDetails("Error", "original failure", null));
        var timer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var retry = new RetryInterceptor<string>(context.Object,
            new WorkflowRetryPolicy(3, TimeSpan.FromSeconds(2), retryTimeout: TimeSpan.FromSeconds(2)),
            () =>
            {
                attempts++;
                now = Start.AddMilliseconds(failureMilliseconds);
                return Task.FromException<string>(failure);
            },
            delay =>
            {
                delays.Add(delay);
                return timer.Task;
            });

        var result = retry.Invoke();
        Assert.False(result.IsCompleted);
        Assert.Equal(TimeSpan.FromMilliseconds(2000 - failureMilliseconds), Assert.Single(delays));
        now = Start.AddSeconds(1.2);
        timer.SetResult();

        Assert.Same(failure, await Assert.ThrowsAsync<WorkflowTaskFailedException>(() => result));
        Assert.Equal(1, attempts);
        Assert.Single(delays);
    }

    [Theory]
    [InlineData("finite")]
    [InlineData("default")]
    [InlineData("infinite")]
    [InlineData("maximum")]
    public async Task NonterminalWait_FiringEarly_AllowsSuccessfulRetry(string timeoutKind)
    {
        var context = new Mock<IWorkflowContext>();
        context.SetupGet(c => c.CurrentUtcDateTime).Returns(Start);
        TimeSpan? timeout = timeoutKind switch
        {
            "finite" => TimeSpan.FromSeconds(10),
            "infinite" => Timeout.InfiniteTimeSpan,
            "maximum" => TimeSpan.MaxValue,
            _ => null
        };
        var attempts = 0;
        var timers = 0;
        var retry = new RetryInterceptor<string>(context.Object,
            new WorkflowRetryPolicy(3, TimeSpan.FromSeconds(1),
                maxRetryInterval: TimeSpan.FromSeconds(1), retryTimeout: timeout),
            () => ++attempts == 1
                ? Task.FromException<string>(new WorkflowTaskFailedException("retry",
                    new WorkflowTaskFailureDetails("Error", "retry", null)))
                : Task.FromResult("done"),
            delay =>
            {
                timers++;
                Assert.Equal(TimeSpan.FromSeconds(1), delay);
                return Task.CompletedTask;
            });

        Assert.Equal("done", await retry.Invoke());
        Assert.Equal(2, attempts);
        Assert.Equal(1, timers);
    }

    [Fact]
    public async Task NonterminalWait_DeliveredAfterDeadline_DoesNotRetry()
    {
        var now = Start;
        var context = new Mock<IWorkflowContext>();
        context.SetupGet(c => c.CurrentUtcDateTime).Returns(() => now);
        var failure = new WorkflowTaskFailedException("retry",
            new WorkflowTaskFailureDetails("Error", "retry", null));
        var attempts = 0;
        var retry = new RetryInterceptor<string>(context.Object,
            new WorkflowRetryPolicy(3, TimeSpan.FromSeconds(1), retryTimeout: TimeSpan.FromSeconds(10)),
            () =>
            {
                attempts++;
                return Task.FromException<string>(failure);
            },
            delay =>
            {
                Assert.Equal(TimeSpan.FromSeconds(1), delay);
                now = Start.AddSeconds(11);
                return Task.CompletedTask;
            });

        Assert.Same(failure, await Assert.ThrowsAsync<WorkflowTaskFailedException>(() => retry.Invoke()));
        Assert.Equal(1, attempts);
    }
}
