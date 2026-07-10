using Dapr.Actors.Next.Abstractions;
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
    private readonly List<ScheduledCallback> callbacks = [];
    private readonly object syncRoot = new();
    private DateTimeOffset utcNow;
    private IActorRuntime? runtime;
    private IActorWireSerializer? serializer;
    private long nextSequence;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActorTimeProvider"/> class.
    /// </summary>
    public VirtualActorTimeProvider(DateTimeOffset? initialUtcNow = null)
    {
        utcNow = initialUtcNow ?? DateTimeOffset.UnixEpoch;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => utcNow;

    /// <summary>
    /// Advances virtual time and enqueues due timer/reminder turns.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Virtual time cannot move backwards.");
        }

        IActorRuntime activeRuntime;
        List<ScheduledCallback> due;
        lock (syncRoot)
        {
            utcNow += duration;
            activeRuntime = runtime ?? throw new InvalidOperationException("Virtual time is not attached to an ActorTestRuntime.");
            _ = serializer ?? throw new InvalidOperationException("Virtual time is not attached to an ActorTestRuntime.");
            due = callbacks
                .Where(callback => !callback.Canceled && callback.DueAt <= utcNow)
                .OrderBy(callback => callback.DueAt)
                .ThenBy(callback => callback.Sequence)
                .ToList();

            foreach (var callback in due)
            {
                while (!callback.Canceled && callback.DueAt <= utcNow)
                {
                    if (callback.ExpiresAt.HasValue && callback.DueAt > callback.ExpiresAt.Value)
                    {
                        callback.Canceled = true;
                        break;
                    }

                    _ = activeRuntime.DispatchAsync(
                        new ActorRuntimeRequest(
                            callback.ActorType,
                            callback.ActorId,
                            callback.OperationName,
                            callback.Kind,
                            callback.Arguments,
                            callback.Headers,
                            new ActorRequestContext(null, null, new Dictionary<string, string>(StringComparer.Ordinal))),
                        CancellationToken.None);

                    if (callback.Period <= TimeSpan.Zero)
                    {
                        callback.Canceled = true;
                        break;
                    }

                    callback.DueAt += callback.Period;
                }
            }
        }
    }

    /// <summary>
    /// Schedules an actor timer turn.
    /// </summary>
    public void ScheduleTimer(string actorType, ActorId actorId, string operationName, TimeSpan dueTime, string argumentsJson = "", IReadOnlyDictionary<string, string>? headers = null) =>
        Schedule(actorType, actorId, operationName, operationName, ActorTurnKind.Timer, dueTime, ToJsonBytes(argumentsJson), argumentsJson, headers, period: null, ttl: null);

    /// <summary>
    /// Schedules an actor reminder turn.
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
        ValidateTimerPeriod(period);
        ValidateTimerTtl(dueTime, ttl);
        Schedule(actorType, actorId, name, operationName, ActorTurnKind.Timer, dueTime, ToJsonBytes(argumentsJson), argumentsJson, headers, period, ttl);
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
        ArgumentNullException.ThrowIfNull(arguments);
        ValidateTimerPeriod(period);
        ValidateTimerTtl(dueTime, ttl);
        var payload = arguments.ToArray();
        Schedule(actorType, actorId, name, operationName, ActorTurnKind.Timer, dueTime, payload, ToJsonText(payload), headers, period, ttl);
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
        var payload = Serialize(arguments);
        ValidateTimerPeriod(period);
        ValidateTimerTtl(dueTime, ttl);
        Schedule(actorType, actorId, name, operationName, ActorTurnKind.Timer, dueTime, payload, ToJsonText(payload), headers, period, ttl);
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
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
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
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
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
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, arguments, period, ttl, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        Cancel(actorType, actorId, name, ActorTurnKind.Timer);
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
        Cancel(actorType, actorId, name, ActorTurnKind.Reminder);
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
            foreach (var callback in callbacks.Where(callback =>
                !callback.Canceled
                && callback.Kind == ActorTurnKind.Reminder
                && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                && (!actorId.HasValue || callback.ActorId.Equals(actorId.Value))))
            {
                callback.Canceled = true;
            }
        }

        return ValueTask.CompletedTask;
    }

    private void Cancel(string actorType, ActorId actorId, string name, ActorTurnKind kind)
    {
        lock (syncRoot)
        {
            foreach (var callback in callbacks.Where(callback =>
                !callback.Canceled
                && callback.Kind == kind
                && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                && callback.ActorId.Equals(actorId)
                && string.Equals(callback.Name, name, StringComparison.Ordinal)))
            {
                callback.Canceled = true;
            }
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

        lock (syncRoot)
        {
            var existing = callbacks
                .Where(callback =>
                    !callback.Canceled
                    && callback.Kind == ActorTurnKind.Reminder
                    && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                    && callback.ActorId.Equals(actorId)
                    && string.Equals(callback.Name, name, StringComparison.Ordinal))
                .ToArray();

            if (existing.Length > 0 && overwrite == false)
            {
                throw new InvalidOperationException($"Reminder '{name}' is already scheduled for actor '{actorType}/{actorId.Value}'.");
            }

            foreach (var callback in existing)
            {
                callback.Canceled = true;
            }
        }

        Schedule(actorType, actorId, name, name, ActorTurnKind.Reminder, dueTime, ToJsonBytes(argumentsJson), argumentsJson, null, period, ttl);
        return ValueTask.CompletedTask;
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var payload = arguments.ToArray();
        return ScheduleReminderCoreAsync(actorType, actorId, name, dueTime, period, payload, ToJsonText(payload), ttl, overwrite);
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
        CancellationToken cancellationToken)
    {
        var payload = Serialize(arguments);
        return ScheduleReminderCoreAsync(actorType, actorId, name, dueTime, period, payload, ToJsonText(payload), ttl, overwrite);
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
        TimeSpan? ttl)
    {
        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime), "Due time cannot be negative.");
        }

        lock (syncRoot)
        {
            callbacks.Add(new ScheduledCallback(
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
                headers ?? new Dictionary<string, string>(StringComparer.Ordinal)));
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
        bool? overwrite)
    {
        if (period < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period cannot be negative.");
        }

        if (ttl.HasValue && ttl.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL cannot be negative.");
        }

        lock (syncRoot)
        {
            var existing = callbacks
                .Where(callback =>
                    !callback.Canceled
                    && callback.Kind == ActorTurnKind.Reminder
                    && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                    && callback.ActorId.Equals(actorId)
                    && string.Equals(callback.Name, name, StringComparison.Ordinal))
                .ToArray();

            if (existing.Length > 0 && overwrite == false)
            {
                throw new InvalidOperationException($"Reminder '{name}' is already scheduled for actor '{actorType}/{actorId.Value}'.");
            }

            foreach (var callback in existing)
            {
                callback.Canceled = true;
            }
        }

        Schedule(actorType, actorId, name, name, ActorTurnKind.Reminder, dueTime, arguments, argumentsJson, null, period, ttl);
        return ValueTask.CompletedTask;
    }

    private byte[] Serialize<TArguments>(TArguments arguments)
    {
        lock (syncRoot)
        {
            return (serializer ?? throw new InvalidOperationException("Virtual time is not attached to an ActorTestRuntime.")).SerializeToBytes(arguments);
        }
    }

    private string? ToJsonText(byte[] arguments)
    {
        lock (syncRoot)
        {
            return serializer?.BytesToJson(arguments) ?? global::System.Text.Encoding.UTF8.GetString(arguments);
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
        IReadOnlyDictionary<string, string> headers)
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

        public bool Canceled { get; set; }

        public ActorReminderInfo ToReminderInfo() =>
            new(ActorType, ActorId, OriginalDueTime, OriginalPeriod, ArgumentsJson, OriginalTtl);
    }
}
