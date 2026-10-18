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

using System.Diagnostics;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Virtual time provider whose timers and reminders fire only when time is explicitly advanced.
/// </summary>
public sealed class VirtualActorTimeProvider : TimeProvider, IActorTimerScheduler, IActorReminderScheduler
{
    private static readonly Lazy<ExecutionContext> EmptyExecutionContext = new(CaptureEmptyExecutionContext);
    private readonly List<ScheduledCallback> callbacks = [];
    private readonly List<VirtualTimer> timers = [];
    private readonly object syncRoot = new();
    private DateTimeOffset utcNow;
    private IActorRuntime? runtime;
    private IActorWireSerializer? serializer;
    private long nextSequence;
    private bool advancing;
    private bool hasCreatedTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActorTimeProvider"/> class.
    /// </summary>
    public VirtualActorTimeProvider(DateTimeOffset? initialUtcNow = null)
    {
        utcNow = initialUtcNow ?? DateTimeOffset.UnixEpoch;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (syncRoot)
        {
            return utcNow;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ValidateTimerTimeout(dueTime, nameof(dueTime));
        ValidateTimerTimeout(period, nameof(period));
        var context = ExecutionContext.Capture() ?? EmptyExecutionContext.Value;
        lock (syncRoot)
        {
            var timer = new VirtualTimer(this, callback, state, context, nextSequence++);
            timer.Change(dueTime, period);
            timers.Add(timer);
            hasCreatedTimer = true;
            return timer;
        }
    }

    /// <summary>
    /// Advances virtual time and enqueues due timer/reminder turns.
    /// </summary>
    /// <remarks>
    /// Callbacks run synchronously, outside the registration lock, in due-time and registration order.
    /// The clock visits each due time before reaching the target; callbacks may change or cancel timers.
    /// Concurrent or recursive advances are rejected. Cancellation prevents future admissions, but an
    /// already admitted callback may finish. A callback exception stops advancement at its due time.
    /// </remarks>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Virtual time cannot move backwards.");
        }

        DateTimeOffset target;
        lock (syncRoot)
        {
            if (advancing)
            {
                throw new InvalidOperationException("Virtual time is already being advanced.");
            }

            // Raw TimeProvider timers can run without an actor runtime, even after disposal.
            // Legacy actor-only use still requires attachment before its first advance.
            if (runtime is null && (!hasCreatedTimer || callbacks.Any(callback => !callback.Canceled)))
            {
                throw new InvalidOperationException("Virtual time is not attached to an ActorTestRuntime.");
            }

            target = utcNow + duration;
            advancing = true;
        }

        try
        {
            while (true)
            {
                VirtualTimer? timer;
                ScheduledCallback? callback;
                IActorRuntime? activeRuntime;
                lock (syncRoot)
                {
                    callbacks.RemoveAll(static item => item.Canceled);
                    callback = callbacks.Where(item => item.DueAt <= target)
                        .OrderBy(static item => item.DueAt).ThenBy(static item => item.Sequence).FirstOrDefault();
                    timer = timers.Where(item => item.DueAt.HasValue && item.DueAt.Value <= target)
                        .OrderBy(static item => item.DueAt).ThenBy(static item => item.Sequence).FirstOrDefault();
                    if (timer is not null && (callback is null || timer.DueAt < callback.DueAt
                            || (timer.DueAt == callback.DueAt && timer.Sequence < callback.Sequence)))
                    {
                        callback = null;
                        utcNow = timer.DueAt!.Value;
                        timer.Admit();
                    }
                    else if (callback is not null)
                    {
                        timer = null;
                        utcNow = callback.DueAt;
                        if (callback.ExpiresAt.HasValue && callback.DueAt > callback.ExpiresAt.Value)
                        {
                            callback.Canceled = true;
                            continue;
                        }

                        if (callback.Period <= TimeSpan.Zero)
                        {
                            callback.Canceled = true;
                        }
                        else
                        {
                            callback.DueAt += callback.Period;
                        }
                    }
                    else
                    {
                        utcNow = target;
                        return;
                    }

                    activeRuntime = runtime;
                }

                if (timer is not null)
                {
                    timer.Invoke();
                }
                else
                {
                    var state = (Provider: this, Runtime: activeRuntime!, Callback: callback!);
                    ExecutionContext.Run(EmptyExecutionContext.Value, static value =>
                    {
                        var invocation = ((VirtualActorTimeProvider Provider, IActorRuntime Runtime, ScheduledCallback Callback))value!;
                        _ = invocation.Provider.DispatchActorCallbackAsync(invocation.Runtime, invocation.Callback);
                    }, state);
                }
            }
        }
        finally
        {
            lock (syncRoot)
            {
                advancing = false;
            }
        }
    }

    /// <summary>
    /// Schedules an actor timer turn, replacing a timer with the same actor and operation name.
    /// </summary>
    public void ScheduleTimer(string actorType, ActorId actorId, string operationName, TimeSpan dueTime, string argumentsJson = "", IReadOnlyDictionary<string, string>? headers = null) =>
        Schedule(actorType, actorId, operationName, operationName, ActorTurnKind.Timer, dueTime, ToJsonBytes(argumentsJson), argumentsJson, headers, period: null, ttl: null);

    /// <summary>
    /// Schedules an actor reminder turn, replacing a reminder with the same actor and operation name.
    /// </summary>
    public void ScheduleReminder(string actorType, ActorId actorId, string operationName, TimeSpan dueTime, string argumentsJson = "", IReadOnlyDictionary<string, string>? headers = null) =>
        Schedule(actorType, actorId, operationName, operationName, ActorTurnKind.Reminder, dueTime, ToJsonBytes(argumentsJson), argumentsJson, headers, period: null, ttl: null);

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTimerPeriod(period);
        ValidateTimerTtl(dueTime, ttl);
        Schedule(actorType, actorId, name, operationName, ActorTurnKind.Timer, dueTime, ToJsonBytes(argumentsJson), argumentsJson, headers, period, ttl, cancellationToken: cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        byte[] arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(arguments);
        ValidateTimerPeriod(period);
        ValidateTimerTtl(dueTime, ttl);
        var payload = arguments.ToArray();
        Schedule(actorType, actorId, name, operationName, ActorTurnKind.Timer, dueTime, payload, ToJsonText(payload), headers, period, ttl, cancellationToken: cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        TArguments arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = Serialize(arguments);
        ValidateTimerPeriod(period);
        ValidateTimerTtl(dueTime, ttl);
        Schedule(actorType, actorId, name, operationName, ActorTurnKind.Timer, dueTime, payload, ToJsonText(payload), headers, period, ttl, cancellationToken: cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask RescheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, argumentsJson, period, ttl, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RescheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        byte[] arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, arguments, period, ttl, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RescheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        TArguments arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, arguments, period, ttl, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        Cancel(actorType, actorId, name, ActorTurnKind.Timer, cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask<ActorReminderInfo?> IActorReminderScheduler.GetAsync(
        string actorType,
        ActorId actorId,
        string name,
        CancellationToken cancellationToken)
    {
        lock (syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var callback = callbacks.LastOrDefault(callback =>
                !callback.Canceled
                && callback.Kind == ActorTurnKind.Reminder
                && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                && callback.ActorId.Equals(actorId)
                && string.Equals(callback.Name, name, StringComparison.Ordinal));

            return ValueTask.FromResult(callback?.ToReminderInfo());
        }
    }

    /// <inheritdoc />
    ValueTask<IReadOnlyList<NamedActorReminderInfo>> IActorReminderScheduler.ListAsync(
        string actorType,
        ActorId? actorId,
        CancellationToken cancellationToken)
    {
        lock (syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reminders = callbacks
                .Where(callback =>
                    !callback.Canceled
                    && callback.Kind == ActorTurnKind.Reminder
                    && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                    && (!actorId.HasValue || callback.ActorId.Equals(actorId.Value)))
                .Select(static callback => new NamedActorReminderInfo(callback.Name, callback.ToReminderInfo()))
                .ToArray();

            return ValueTask.FromResult<IReadOnlyList<NamedActorReminderInfo>>(reminders);
        }
    }

    /// <inheritdoc />
    ValueTask IActorReminderScheduler.CancelAsync(
        string actorType,
        ActorId actorId,
        string name,
        CancellationToken cancellationToken)
    {
        Cancel(actorType, actorId, name, ActorTurnKind.Reminder, cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IActorReminderScheduler.CancelAllAsync(
        string actorType,
        ActorId? actorId,
        CancellationToken cancellationToken)
    {
        lock (syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var callback in callbacks.Where(callback =>
                !callback.Canceled
                && callback.Kind == ActorTurnKind.Reminder
                && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                && (!actorId.HasValue || callback.ActorId.Equals(actorId.Value))))
            {
                callback.Canceled = true;
            }

            callbacks.RemoveAll(static callback => callback.Canceled);
        }

        return ValueTask.CompletedTask;
    }

    private void Cancel(string actorType, ActorId actorId, string name, ActorTurnKind kind, CancellationToken cancellationToken)
    {
        lock (syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var callback in callbacks.Where(callback =>
                !callback.Canceled
                && callback.Kind == kind
                && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                && callback.ActorId.Equals(actorId)
                && string.Equals(callback.Name, name, StringComparison.Ordinal)))
            {
                callback.Canceled = true;
            }

            callbacks.RemoveAll(static callback => callback.Canceled);
        }
    }

    /// <inheritdoc />
    ValueTask IActorReminderScheduler.ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        string argumentsJson,
        TimeSpan? ttl,
        bool? overwrite,
        ActorReminderFailurePolicy? failurePolicy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ScheduleReminderCoreAsync(actorType, actorId, name, dueTime, period, ToJsonBytes(argumentsJson),
            argumentsJson, ttl, overwrite, failurePolicy, cancellationToken);
    }

    /// <inheritdoc />
    ValueTask IActorReminderScheduler.ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        byte[] arguments,
        TimeSpan? ttl,
        bool? overwrite,
        ActorReminderFailurePolicy? failurePolicy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(arguments);
        var payload = arguments.ToArray();
        return ScheduleReminderCoreAsync(actorType, actorId, name, dueTime, period, payload, ToJsonText(payload), ttl, overwrite, failurePolicy, cancellationToken);
    }

    /// <inheritdoc />
    ValueTask IActorReminderScheduler.ScheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        TArguments arguments,
        TimeSpan? ttl,
        bool? overwrite,
        ActorReminderFailurePolicy? failurePolicy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = Serialize(arguments);
        return ScheduleReminderCoreAsync(actorType, actorId, name, dueTime, period, payload, ToJsonText(payload), ttl, overwrite, failurePolicy, cancellationToken);
    }

    internal void Attach(IActorRuntime actorRuntime, IActorWireSerializer wireSerializer)
    {
        lock (syncRoot)
        {
            runtime = actorRuntime;
            serializer = wireSerializer;
        }
    }

    private void Schedule(
        string actorType,
        ActorId actorId,
        string name,
        string operationName,
        ActorTurnKind kind,
        TimeSpan dueTime,
        byte[] arguments,
        string? argumentsJson,
        IReadOnlyDictionary<string, string>? headers,
        TimeSpan? period,
        TimeSpan? ttl,
        ActorReminderFailurePolicy? failurePolicy = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime), "Due time cannot be negative.");
        }

        lock (syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = callbacks.Where(callback => !callback.Canceled
                && callback.Kind == kind
                && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                && callback.ActorId.Equals(actorId)
                && string.Equals(callback.Name, name, StringComparison.Ordinal)).ToArray();
            if (existing.Length > 0 && overwrite == false)
            {
                throw new ActorReminderAlreadyExistsException(actorType, actorId.Value, name);
            }

            var registration = new ScheduledCallback(
                actorType,
                actorId,
                name,
                operationName,
                kind,
                utcNow + dueTime,
                period.GetValueOrDefault(Timeout.InfiniteTimeSpan),
                ttl.HasValue ? utcNow + ttl.Value : null,
                dueTime,
                period,
                ttl,
                nextSequence++,
                arguments,
                argumentsJson,
                headers is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(headers),
                failurePolicy);
            foreach (var callback in existing)
            {
                callback.Canceled = true;
            }

            callbacks.RemoveAll(static callback => callback.Canceled);
            callbacks.Add(registration);
        }
    }

    private ValueTask ScheduleReminderCoreAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        byte[] arguments,
        string? argumentsJson,
        TimeSpan? ttl,
        bool? overwrite,
        ActorReminderFailurePolicy? failurePolicy,
        CancellationToken cancellationToken)
    {
        if (period < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period cannot be negative.");
        }

        if (ttl.HasValue && ttl.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL cannot be negative.");
        }

        ValidateFailurePolicy(failurePolicy);

        Schedule(actorType, actorId, name, name, ActorTurnKind.Reminder, dueTime, arguments, argumentsJson,
            null, period, ttl, failurePolicy, overwrite, cancellationToken);
        return ValueTask.CompletedTask;
    }

    private byte[] Serialize<TArguments>(TArguments arguments)
    {
        IActorWireSerializer activeSerializer;
        lock (syncRoot)
        {
            activeSerializer = serializer ?? throw new InvalidOperationException("Virtual time is not attached to an ActorTestRuntime.");
        }

        return activeSerializer.SerializeToBytes(arguments);
    }

    private string? ToJsonText(byte[] arguments)
    {
        IActorWireSerializer? activeSerializer;
        lock (syncRoot)
        {
            activeSerializer = serializer;
        }

        return activeSerializer?.BytesToJson(arguments) ?? global::System.Text.Encoding.UTF8.GetString(arguments);
    }

    private async Task DispatchActorCallbackAsync(IActorRuntime activeRuntime, ScheduledCallback callback)
    {
        try
        {
            await activeRuntime.DispatchAsync(
                new ActorRuntimeRequest(callback.ActorType, callback.ActorId, callback.OperationName, callback.Kind,
                    callback.Arguments, callback.Headers,
                    new ActorRequestContext(null, null, new Dictionary<string, string>(StringComparer.Ordinal))),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.TraceError("Virtual actor callback failed for {0}/{1}/{2}: {3}",
                callback.ActorType, callback.ActorId, callback.OperationName, exception);
        }
    }

    private static ExecutionContext CaptureEmptyExecutionContext()
    {
        var completion = new TaskCompletionSource<ExecutionContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        ThreadPool.UnsafeQueueUserWorkItem(static state =>
            state.SetResult(ExecutionContext.Capture()!), completion, preferLocal: false);
        return completion.Task.GetAwaiter().GetResult();
    }

    private static void ValidateTimerTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout != Timeout.InfiniteTimeSpan && (timeout < TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Timer timeout must be nonnegative, at most 4294967294 milliseconds, or infinite.");
        }
    }

    private static byte[] ToJsonBytes(string? argumentsJson) =>
        string.IsNullOrEmpty(argumentsJson) ? [] : global::System.Text.Encoding.UTF8.GetBytes(argumentsJson);

    private static void ValidateTimerPeriod(TimeSpan? period)
    {
        if (period.HasValue && period.Value < Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period cannot be less than Timeout.InfiniteTimeSpan.");
        }
    }

    private static void ValidateTimerTtl(TimeSpan dueTime, TimeSpan? ttl)
    {
        if (ttl.HasValue && (ttl.Value < TimeSpan.Zero || ttl.Value < dueTime))
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL cannot be negative or earlier than the due time.");
        }
    }

    private static void ValidateFailurePolicy(ActorReminderFailurePolicy? failurePolicy)
    {
        if (failurePolicy is ActorReminderFailurePolicy.Constant constantPolicy && constantPolicy.Interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(failurePolicy), "Failure policy interval cannot be negative.");
        }
    }

    private sealed class VirtualTimer(
        VirtualActorTimeProvider owner,
        TimerCallback callback,
        object? state,
        ExecutionContext context,
        long sequence) : ITimer
    {
        private bool disposed;
        private int activeCallbacks;
        private TimeSpan period;
        private TaskCompletionSource? disposal;

        public DateTimeOffset? DueAt { get; private set; }

        public long Sequence { get; } = sequence;

        public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
        {
            ValidateTimerTimeout(dueTime, nameof(dueTime));
            ValidateTimerTimeout(newPeriod, nameof(newPeriod));
            lock (owner.syncRoot)
            {
                if (disposed)
                {
                    return false;
                }

                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.utcNow + dueTime;
                period = newPeriod;
                return true;
            }
        }

        public void Admit()
        {
            DueAt = period > TimeSpan.Zero ? DueAt + period : null;
            activeCallbacks++;
        }

        public void Invoke()
        {
            try
            {
                ExecutionContext.Run(context, _ => callback(state), null);
            }
            finally
            {
                lock (owner.syncRoot)
                {
                    activeCallbacks--;
                    if (activeCallbacks == 0)
                    {
                        disposal?.TrySetResult();
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (owner.syncRoot)
            {
                disposed = true;
                DueAt = null;
                owner.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            lock (owner.syncRoot)
            {
                Dispose();
                if (activeCallbacks == 0)
                {
                    return ValueTask.CompletedTask;
                }

                disposal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                return new ValueTask(disposal.Task);
            }
        }
    }

    private sealed class ScheduledCallback(
        string actorType,
        ActorId actorId,
        string name,
        string operationName,
        ActorTurnKind kind,
        DateTimeOffset dueAt,
        TimeSpan period,
        DateTimeOffset? expiresAt,
        TimeSpan originalDueTime,
        TimeSpan? originalPeriod,
        TimeSpan? originalTtl,
        long sequence,
        byte[] arguments,
        string? argumentsJson,
        IReadOnlyDictionary<string, string> headers,
        ActorReminderFailurePolicy? failurePolicy)
    {
        public string ActorType { get; } = actorType;

        public ActorId ActorId { get; } = actorId;

        public string Name { get; } = name;

        public string OperationName { get; } = operationName;

        public ActorTurnKind Kind { get; } = kind;

        public DateTimeOffset DueAt { get; set; } = dueAt;

        public TimeSpan Period { get; } = period;

        public DateTimeOffset? ExpiresAt { get; } = expiresAt;

        public TimeSpan OriginalDueTime { get; } = originalDueTime;

        public TimeSpan? OriginalPeriod { get; } = originalPeriod;

        public TimeSpan? OriginalTtl { get; } = originalTtl;

        public long Sequence { get; } = sequence;

        public byte[] Arguments { get; } = arguments;

        public string? ArgumentsJson { get; } = argumentsJson;

        public IReadOnlyDictionary<string, string> Headers { get; } = headers;

        public ActorReminderFailurePolicy? FailurePolicy { get; } = failurePolicy;

        public bool Canceled { get; set; }

        public ActorReminderInfo ToReminderInfo() =>
            new(ActorType, ActorId, OriginalDueTime, OriginalPeriod, ArgumentsJson, OriginalTtl, FailurePolicy);
    }
}
