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

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowContextWaitForExternalEventTests
{
    private sealed class ExternalEventProbeContext<TEventPayload> : WorkflowContext
    {
        private readonly DateTime _now;

        public ExternalEventProbeContext(DateTime now)
        {
            _now = now;
        }

        public TaskCompletionSource<TEventPayload> EventTcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<object?> TimerTcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DateTime? LastTimerFireAt { get; private set; }
        public CancellationToken LastTimerToken { get; private set; }
        public CancellationToken LastEventToken { get; private set; }

        public override string Name => "wf";
        public override string InstanceId => "id-1";
        public override DateTime CurrentUtcDateTime => _now;
        public override bool IsReplaying => false;
        public override bool IsPatched(string patchName) => false;

        public override Task<TEvent> WaitForExternalEventAsync<TEvent>(string eventName, CancellationToken cancellationToken = default)
        {
            LastEventToken = cancellationToken;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => EventTcs.TrySetCanceled(cancellationToken));
            }

            if (typeof(TEvent) != typeof(TEventPayload))
            {
                throw new NotSupportedException($"Unsupported event type: {typeof(TEvent).FullName}");
            }

            return (Task<TEvent>)(object)EventTcs.Task;
        }

        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        {
            LastTimerFireAt = fireAt;
            LastTimerToken = cancellationToken;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => TimerTcs.TrySetCanceled(cancellationToken));
            }

            return TimerTcs.Task;
        }

        public override Task<T> CallActivityAsync<T>(string name, object? input = null, WorkflowTaskOptions? options = null)
            => Task.FromResult(default(T)!);
        public override void SendEvent(string instanceId, string eventName, object payload) { }
        public override void SetCustomStatus(object? customStatus) { }
        public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null, ChildWorkflowTaskOptions? options = null)
            => Task.FromResult(default(TResult)!);
        public override Microsoft.Extensions.Logging.ILogger CreateReplaySafeLogger(string categoryName) => new NullLogger();
        public override Microsoft.Extensions.Logging.ILogger CreateReplaySafeLogger(Type type) => new NullLogger();
        public override Microsoft.Extensions.Logging.ILogger CreateReplaySafeLogger<TLogger>() => new NullLogger();
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) { }
        public override Guid NewGuid() => Guid.Empty;
        public override PropagatedHistory? GetPropagatedHistory() => null;

        private sealed class NullLogger : Microsoft.Extensions.Logging.ILogger
        {
            IDisposable? Microsoft.Extensions.Logging.ILogger.BeginScope<TState>(TState state) => null;
            bool Microsoft.Extensions.Logging.ILogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
            void Microsoft.Extensions.Logging.ILogger.Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }
    }

    [Fact]
    public async Task WaitForExternalEventAsync_WithTimeout_Returns_Event_When_Event_Wins()
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ctx = new ExternalEventProbeContext<string>(now);
        var timeout = TimeSpan.FromMinutes(1);

        Task<string> task = ctx.WaitForExternalEventAsync<string>("evt", timeout);

        Assert.Equal(now.Add(timeout), ctx.LastTimerFireAt);

        ctx.EventTcs.TrySetResult("payload");
        var result = await task;

        Assert.Equal("payload", result);
        Assert.False(ctx.LastEventToken.IsCancellationRequested);
        Assert.True(ctx.LastTimerToken.IsCancellationRequested);
        Assert.True(ctx.TimerTcs.Task.IsCanceled);
    }

    [Fact]
    public async Task WaitForExternalEventAsync_WithTimeout_Throws_When_Timer_Wins()
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ctx = new ExternalEventProbeContext<string>(now);
        var timeout = TimeSpan.FromMinutes(1);

        Task<string> task = ctx.WaitForExternalEventAsync<string>("evt", timeout);

        ctx.TimerTcs.TrySetResult(null);

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);

        Assert.True(ctx.LastEventToken.IsCancellationRequested);
        Assert.True(ctx.EventTcs.Task.IsCanceled);
        Assert.False(ctx.LastTimerToken.IsCancellationRequested);
    }
}
