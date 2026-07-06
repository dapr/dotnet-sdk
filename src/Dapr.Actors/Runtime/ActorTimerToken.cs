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

using System;

namespace Dapr.Actors.Runtime;

/// <summary>
/// Represents the timer set for an actor.
/// </summary>
public class ActorTimerToken
{
    /// <summary>
    /// Initializes a new <see cref="ActorTimerToken" />.
    /// </summary>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="name">The timer name.</param>
    public ActorTimerToken(
        string actorType,
        ActorId actorId,
        string name)
    {
        if (actorType == null)
        {
            throw new ArgumentNullException(nameof(actorType));
        }

        if (actorId == null)
        {
            throw new ArgumentNullException(nameof(actorId));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        this.ActorType = actorType;
        this.ActorId = actorId;
        this.Name = name;
    }

    /// <summary>
    /// Gets the actor type.
    /// </summary>
    public string ActorType { get; }

    /// <summary>
    /// Gets the actor id.
    /// </summary>
    public ActorId ActorId { get; }

    /// <summary>
    /// Gets the timer name.
    /// </summary>
    public string Name { get; }
}