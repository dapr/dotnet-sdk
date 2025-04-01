// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Actors.Runtime;

using System;
using System.Threading.Tasks;

/// <summary>
/// Interface that actors must implement to consume reminders registered using RegisterReminderAsync. />.
/// </summary>
public interface IRemindable
{
    /// <summary>
    /// The reminder call back invoked when an actor reminder is triggered.
    /// </summary>
    /// <param name="reminderName">The name of reminder provided during registration.</param>
    /// <param name="state">The user state provided during registration.</param>
    /// <param name="dueTime">The invocation due time provided during registration.</param>
    /// <param name="period">The invocation period provided during registration.</param>
    /// <returns>A task that represents the asynchronous operation performed by this callback.</returns>
    /// <remarks>
    /// <para>The state of this actor is saved by the actor runtime upon completion of the task returned by this method. If an error occurs while saving the state, then all state cached by this actor's <see cref="Dapr.Actors.Runtime.Actor.StateManager" /> will be discarded and reloaded from previously saved state when the next actor method or reminder invocation occurs.
    /// </para>
    /// </remarks>
    Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period);
}