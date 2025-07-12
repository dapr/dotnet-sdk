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

/// <summary>
/// A state object created by an implementation of <see cref="ActorActivator" />. Implementations
/// can return a subclass of <see cref="ActorActivatorState" /> to associate additional data
/// with an Actor instance.
/// </summary>
public class ActorActivatorState
{
    /// <summary>
    /// Initializes a new instance of <see cref="ActorActivatorState" />.
    /// </summary>
    /// <param name="actor">The <see cref="Actor" /> instance.</param>
    public ActorActivatorState(Actor actor)
    {
        Actor = actor;
    }

    /// <summary>
    /// Gets the <see cref="Actor" /> instance.
    /// </summary>
    public Actor Actor { get; }
}