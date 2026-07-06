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

using System;
using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.Timers;

/// <summary>
/// Options used to configure a timer started by <see cref="ITimerActor"/>.
/// </summary>
public sealed class StartTimerOptions
{
    /// <summary>
    /// Gets or sets the total number of ticks after which the timer self-cancels.
    /// </summary>
    public int Total { get; set; }
}

/// <summary>
/// Captures the state maintained by <see cref="ITimerActor"/>.
/// </summary>
public sealed class TimerState
{
    /// <summary>
    /// Gets or sets the number of times the timer callback has fired.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the timer is currently active.
    /// </summary>
    public bool IsTimerRunning { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last timer invocation.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the name of the currently registered timer, used for self-cancellation.
    /// </summary>
    public string? ActiveTimerName { get; set; }
}

/// <summary>
/// Actor interface that exercises Dapr timer registration and management.
/// </summary>
public interface ITimerActor : IPingActor, IActor
{
    /// <summary>
    /// Starts a self-limiting timer that fires <see cref="StartTimerOptions.Total"/> times.
    /// </summary>
    /// <param name="options">Timer configuration.</param>
    Task StartTimer(StartTimerOptions options);

    /// <summary>
    /// Starts a timer that expires after <paramref name="ttl"/>.
    /// </summary>
    /// <param name="ttl">The time-to-live for the timer.</param>
    Task StartTimerWithTtl(TimeSpan ttl);

    /// <summary>
    /// Returns the current timer state.
    /// </summary>
    Task<TimerState> GetState();
}
