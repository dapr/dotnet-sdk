// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using System;
using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.Reminders;

/// <summary>
/// Options for starting a reminder with a finite count.
/// </summary>
public sealed class StartReminderOptions
{
    /// <summary>Gets or sets the total number of times the reminder should fire before stopping itself.</summary>
    public int Total { get; set; }
}

/// <summary>
/// Captures the state maintained by <see cref="IReminderActor"/>.
/// </summary>
public sealed class ReminderState
{
    /// <summary>Gets or sets the number of times the reminder has fired.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets a value indicating whether the reminder is currently running.</summary>
    public bool IsReminderRunning { get; set; }

    /// <summary>Gets or sets the timestamp of the last reminder invocation.</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Actor interface that exercises Dapr reminder registration and management.
/// </summary>
public interface IReminderActor : IPingActor, IActor
{
    /// <summary>Starts a self-limiting reminder that fires <see cref="StartReminderOptions.Total"/> times.</summary>
    /// <param name="options">Reminder configuration.</param>
    Task StartReminder(StartReminderOptions options);

    /// <summary>Starts a reminder that expires after <paramref name="ttl"/>.</summary>
    /// <param name="ttl">The time-to-live for the reminder.</param>
    Task StartReminderWithTtl(TimeSpan ttl);

    /// <summary>Starts a reminder that fires exactly <paramref name="repetitions"/> times.</summary>
    /// <param name="repetitions">The maximum number of reminder invocations.</param>
    Task StartReminderWithRepetitions(int repetitions);

    /// <summary>Starts a reminder that fires at most <paramref name="repetitions"/> times and expires after <paramref name="ttl"/>.</summary>
    /// <param name="ttl">The time-to-live for the reminder.</param>
    /// <param name="repetitions">The maximum number of reminder invocations.</param>
    Task StartReminderWithTtlAndRepetitions(TimeSpan ttl, int repetitions);

    /// <summary>Returns the current reminder state.</summary>
    Task<ReminderState> GetState();

    /// <summary>Returns the serialized JSON representation of the active reminder, or <c>"null"</c> when none is registered.</summary>
    Task<string> GetReminder();
}
