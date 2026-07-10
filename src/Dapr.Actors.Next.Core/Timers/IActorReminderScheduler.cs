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

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Schedules durable actor reminder turns through the runtime.
/// </summary>
public interface IActorReminderScheduler
{
    /// <summary>
    /// Registers or updates a durable actor reminder.
    /// </summary>
    ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        string argumentsJson,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or updates a durable actor reminder with a caller-provided wire payload.
    /// </summary>
    ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        byte[] arguments,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or updates a durable actor reminder with a typed payload serialized by the configured actor wire serializer.
    /// </summary>
    ValueTask ScheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        TArguments arguments,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a durable actor reminder registration by name.
    /// </summary>
    ValueTask<ActorReminderInfo?> GetAsync(
        string actorType,
        ActorId actorId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists durable actor reminders for an actor type, optionally scoped to one actor id.
    /// </summary>
    ValueTask<IReadOnlyList<NamedActorReminderInfo>> ListAsync(
        string actorType,
        ActorId? actorId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a durable actor reminder.
    /// </summary>
    ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels all durable actor reminders for an actor type, optionally scoped to one actor id.
    /// </summary>
    ValueTask CancelAllAsync(string actorType, ActorId? actorId = null, CancellationToken cancellationToken = default);
}
