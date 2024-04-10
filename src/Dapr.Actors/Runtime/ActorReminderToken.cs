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

namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// Represents a reminder that can be unregistered by an actor.
    /// </summary>
    public class ActorReminderToken
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ActorReminderToken" />.
        /// </summary>
        /// <param name="actorType">The actor type.</param>
        /// <param name="actorId">The actor id.</param>
        /// <param name="name">The reminder name.</param>
        public ActorReminderToken(
            string? actorType,
            ActorId actorId,
            string name)
        {
            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            this.ActorType = actorType ?? throw new ArgumentNullException(nameof(actorType));
            this.ActorId = actorId;
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the actor type.
        /// </summary>
        public string? ActorType { get; }

        /// <summary>
        /// Gets the actor id.
        /// </summary>
        public ActorId ActorId { get; }

        /// <summary>
        /// Gets the reminder name.
        /// </summary>
        public string Name { get; }
    }
}
