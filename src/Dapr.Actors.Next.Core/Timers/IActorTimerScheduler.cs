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
/// Schedules actor timer turns through the runtime.
/// </summary>
public interface IActorTimerScheduler
{
    /// <summary>
    /// Schedules a named actor timer.
    /// </summary>
    ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a named actor timer with a caller-provided wire payload.
    /// </summary>
    ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        byte[] arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a named actor timer with a typed payload serialized by the configured actor wire serializer.
    /// </summary>
    ValueTask ScheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        TArguments arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules a named actor timer.
    /// </summary>
    ValueTask RescheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules a named actor timer with a caller-provided wire payload.
    /// </summary>
    ValueTask RescheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        byte[] arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules a named actor timer with a typed payload serialized by the configured actor wire serializer.
    /// </summary>
    ValueTask RescheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        TArguments arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a named actor timer registration, or <see langword="null"/> when it does not exist.
    /// Timers are host-local: the call must reach the sidecar that currently hosts the actor.
    /// </summary>
    ValueTask<ActorTimerInfo?> GetAsync(
        string actorType,
        ActorId actorId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the timers registered for one actor.
    /// Timers are host-local: the call must reach the sidecar that currently hosts the actor.
    /// </summary>
    ValueTask<IReadOnlyList<NamedActorTimerInfo>> ListAsync(
        string actorType,
        ActorId actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a named actor timer.
    /// </summary>
    ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default);
}
