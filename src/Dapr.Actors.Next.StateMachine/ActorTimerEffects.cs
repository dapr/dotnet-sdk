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

using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Effect-context timer surface backed by the Core actor timer scheduler.
/// </summary>
internal sealed class ActorTimerEffects(
    IActorTimerScheduler scheduler,
    string actorType,
    ActorId actorId,
    Action<TimeSpan, bool> scheduleTimeout,
    Action cancelTimeout) : IActorTimerEffects
{
    /// <inheritdoc />
    public void Schedule(string name, TimeSpan dueTime) => ScheduleCore(name, dueTime, reschedule: false);

    /// <inheritdoc />
    public void Reschedule(string name, TimeSpan dueTime) => ScheduleCore(name, dueTime, reschedule: true);

    /// <inheritdoc />
    public void Cancel(string name)
    {
        if (string.Equals(name, StateMachineConstants.StateTimeoutTimerName, StringComparison.Ordinal))
        {
            cancelTimeout();
            return;
        }
        scheduler.CancelAsync(actorType, actorId, name).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Schedules or reschedules a named timer with the serialized state-machine timer payload.
    /// </summary>
    private void ScheduleCore(string name, TimeSpan dueTime, bool reschedule)
    {
        if (string.Equals(name, StateMachineConstants.StateTimeoutTimerName, StringComparison.Ordinal))
        {
            scheduleTimeout(dueTime, reschedule);
            return;
        }
        var payload = JsonSerializer.Serialize(new StateMachineTimerPayload(name, null));
        if (reschedule)
        {
            scheduler.RescheduleAsync(actorType, actorId, name, dueTime, StateMachineConstants.TimerOperationName, payload).AsTask().GetAwaiter().GetResult();
        }
        else
        {
            scheduler.ScheduleAsync(actorType, actorId, name, dueTime, StateMachineConstants.TimerOperationName, payload).AsTask().GetAwaiter().GetResult();
        }
    }
}

/// <summary>
/// Serialized payload delivered by the reserved state-machine timer operation.
/// </summary>
internal sealed record StateMachineTimerPayload
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineTimerPayload"/> record.
    /// </summary>
    public StateMachineTimerPayload(string name, string? state, Guid? generation = null)
    {
        Name = name;
        State = state;
        Generation = generation;
    }

    /// <summary>
    /// Gets the timer name that fired.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the state name expected for a declarative state timeout.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets the registration identity used to reject superseded timeout callbacks.
    /// </summary>
    public Guid? Generation { get; init; }
}
