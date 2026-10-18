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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// In-memory reminder scheduler backed by <see cref="TimeProvider"/> and actor reminder turns.
/// </summary>
/// <remarks>
/// Registration, overwrite checks, cancellation, and disposal are serialized. Cancellation or
/// replacement prevents new callback admissions; an already admitted actor turn may finish.
/// Callbacks do not inherit the registering actor's execution context. A disposed scheduler
/// rejects further operations.
/// </remarks>
public sealed class CoreActorReminderScheduler(
    IActorRuntime runtime,
    IActorWireSerializer serializer,
    TimeProvider timeProvider,
    ILogger<CoreActorReminderScheduler>? logger) : IActorReminderScheduler, IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<ReminderKey, ReminderRegistration> reminders = [];
    private bool disposed;

    /// <summary>
    /// Initializes a reminder scheduler with trace-based callback failure reporting.
    /// </summary>
    public CoreActorReminderScheduler(IActorRuntime runtime, IActorWireSerializer serializer, TimeProvider timeProvider)
        : this(runtime, serializer, timeProvider, null)
    {
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        string argumentsJson,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        ActorReminderFailurePolicy? failurePolicy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            serializer.JsonToBytes(argumentsJson),
            argumentsJson,
            ttl,
            overwrite,
            failurePolicy,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        byte[] arguments,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        ActorReminderFailurePolicy? failurePolicy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(arguments);
        var payload = arguments.ToArray();
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            payload,
            serializer.BytesToJson(payload),
            ttl,
            overwrite,
            failurePolicy,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        TArguments arguments,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        ActorReminderFailurePolicy? failurePolicy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = serializer.SerializeToBytes(arguments);
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            payload,
            serializer.BytesToJson(payload),
            ttl,
            overwrite,
            failurePolicy,
            cancellationToken);
    }

    private ValueTask ScheduleCoreAsync(
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
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime), "Due time cannot be negative.");
        }

        if (period < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period cannot be negative.");
        }

        if (ttl.HasValue && ttl.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL cannot be negative.");
        }

        if (failurePolicy is ActorReminderFailurePolicy.Constant constantPolicy && constantPolicy.Interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(failurePolicy), "Failure policy interval cannot be negative.");
        }

        var key = new ReminderKey(actorType, actorId.Value, name);
        var info = new ActorReminderInfo(actorType, actorId, dueTime, period, argumentsJson, ttl, failurePolicy);
        var request = new ActorRuntimeRequest(
            actorType, actorId, name, ActorTurnKind.Reminder, arguments, ActorHeaders.Empty,
            new ActorRequestContext(null, null, ActorHeaders.Empty));
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            reminders.TryGetValue(key, out var existing);
            if (existing is not null && overwrite == false)
            {
                throw new ActorReminderAlreadyExistsException(actorType, actorId.Value, name);
            }

            var timer = new ScheduledActorCallback(sync, runtime, timeProvider, request, null, logger);
            reminders[key] = new ReminderRegistration(timer, info);
            try
            {
                timer.Start(dueTime, period == TimeSpan.Zero ? Timeout.InfiniteTimeSpan : period);
            }
            catch
            {
                timer.Stop();
                if (existing is null)
                {
                    reminders.Remove(key);
                }
                else
                {
                    reminders[key] = existing;
                }

                throw;
            }

            existing?.Timer.Stop();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ActorReminderInfo?> GetAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            return ValueTask.FromResult(
                reminders.TryGetValue(new ReminderKey(actorType, actorId.Value, name), out var registration)
                    ? registration.Info
                    : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<NamedActorReminderInfo>> ListAsync(string actorType, ActorId? actorId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            var result = reminders
                .Where(pair =>
                    string.Equals(pair.Key.ActorType, actorType, StringComparison.Ordinal)
                    && (!actorId.HasValue || string.Equals(pair.Key.ActorId, actorId.Value.Value, StringComparison.Ordinal)))
                .Select(static pair => new NamedActorReminderInfo(pair.Key.Name, pair.Value.Info))
                .ToArray();

            return ValueTask.FromResult<IReadOnlyList<NamedActorReminderInfo>>(result);
        }
    }

    /// <inheritdoc />
    public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (reminders.Remove(new ReminderKey(actorType, actorId.Value, name), out var registration))
            {
                registration.Timer.Stop();
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CancelAllAsync(string actorType, ActorId? actorId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (var pair in reminders.ToArray())
            {
                if (string.Equals(pair.Key.ActorType, actorType, StringComparison.Ordinal)
                    && (!actorId.HasValue || string.Equals(pair.Key.ActorId, actorId.Value.Value, StringComparison.Ordinal)))
                {
                    reminders.Remove(pair.Key);
                    pair.Value.Timer.Stop();
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (var reminder in reminders.Values)
            {
                reminder.Timer.Stop();
            }

            reminders.Clear();
        }
    }

    private sealed record ReminderRegistration(ScheduledActorCallback Timer, ActorReminderInfo Info);

    private readonly record struct ReminderKey(string ActorType, string ActorId, string Name);
}
