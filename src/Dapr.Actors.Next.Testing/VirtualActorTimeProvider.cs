using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Virtual time provider whose timers and reminders fire only when time is explicitly advanced.
/// </summary>
public sealed class VirtualActorTimeProvider : TimeProvider, IActorTimerScheduler
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
        IActorWireSerializer activeSerializer;
        List<ScheduledCallback> due;
        lock (syncRoot)
        {
            utcNow += duration;
            activeRuntime = runtime ?? throw new InvalidOperationException("Virtual time is not attached to an ActorTestRuntime.");
            activeSerializer = serializer ?? throw new InvalidOperationException("Virtual time is not attached to an ActorTestRuntime.");
            due = callbacks
                .Where(callback => !callback.Canceled && callback.Task is null && callback.DueAt <= utcNow)
                .OrderBy(callback => callback.DueAt)
                .ThenBy(callback => callback.Sequence)
                .ToList();

            foreach (var callback in due)
            {
                callback.Task = activeRuntime.DispatchAsync(
                    new ActorRuntimeRequest(
                        callback.ActorType,
                        callback.ActorId,
                        callback.OperationName,
                        callback.Kind,
                        activeSerializer.JsonToBytes(callback.ArgumentsJson),
                        callback.Headers,
                        new ActorRequestContext(null, null, new Dictionary<string, string>(StringComparer.Ordinal))),
                    CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Schedules an actor timer turn.
    /// </summary>
    public void ScheduleTimer(string actorType, ActorId actorId, string operationName, TimeSpan dueTime, string argumentsJson = "", IReadOnlyDictionary<string, string>? headers = null) =>
        Schedule(actorType, actorId, operationName, operationName, ActorTurnKind.Timer, dueTime, argumentsJson, headers);

    /// <summary>
    /// Schedules an actor reminder turn.
    /// </summary>
    public void ScheduleReminder(string actorType, ActorId actorId, string operationName, TimeSpan dueTime, string argumentsJson = "", IReadOnlyDictionary<string, string>? headers = null) =>
        Schedule(actorType, actorId, operationName, operationName, ActorTurnKind.Reminder, dueTime, argumentsJson, headers);

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        Schedule(actorType, actorId, name, operationName, ActorTurnKind.Timer, dueTime, argumentsJson, headers);
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
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, argumentsJson, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            foreach (var callback in callbacks.Where(callback =>
                callback.Task is null
                && string.Equals(callback.ActorType, actorType, StringComparison.Ordinal)
                && callback.ActorId.Equals(actorId)
                && string.Equals(callback.Name, name, StringComparison.Ordinal)))
            {
                callback.Canceled = true;
            }
        }

        return ValueTask.CompletedTask;
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
        string argumentsJson,
        IReadOnlyDictionary<string, string>? headers)
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
                nextSequence++,
                argumentsJson,
                headers ?? new Dictionary<string, string>(StringComparer.Ordinal)));
        }
    }

    private sealed record ScheduledCallback(
        string ActorType,
        ActorId ActorId,
        string Name,
        string OperationName,
        ActorTurnKind Kind,
        DateTimeOffset DueAt,
        long Sequence,
        string ArgumentsJson,
        IReadOnlyDictionary<string, string> Headers)
    {
        public Task<byte[]?>? Task { get; set; }

        public bool Canceled { get; set; }
    }
}
