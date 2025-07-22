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

/// <summary>
/// Represents a reminder registered using <see cref="Dapr.Actors.Runtime.Actor.RegisterReminderAsync(ActorReminderOptions)" />.
/// </summary>
public interface IActorReminder
{
    /// <summary>
    /// Gets the name of the reminder. The name is unique per actor.
    /// </summary>
    /// <value>The name of the reminder.</value>
    string Name { get; }

    /// <summary>
    /// Gets the time when the reminder is first due to be invoked.
    /// </summary>
    /// <value>The time when the reminder is first due to be invoked.</value>
    /// <remarks>
    /// A value of negative one (-1) milliseconds means the reminder is not invoked. A value of zero (0) means the reminder is invoked immediately after registration.
    /// </remarks>
    TimeSpan DueTime { get; }

    /// <summary>
    /// Gets the time interval at which the reminder is invoked periodically.
    /// </summary>
    /// <value>The time interval at which the reminder is invoked periodically.</value>
    /// <remarks>
    /// The first invocation of the reminder occurs after <see cref="Dapr.Actors.Runtime.IActorReminder.DueTime" />. All subsequent invocations occur at intervals defined by this property.
    /// </remarks>
    TimeSpan Period { get; }

    /// <summary>
    /// Gets the user state passed to the reminder invocation.
    /// </summary>
    /// <value>The user state passed to the reminder invocation.</value>
    byte[] State { get; }
}