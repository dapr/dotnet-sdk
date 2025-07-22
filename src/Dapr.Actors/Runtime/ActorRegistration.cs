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
/// Represents an actor type registered with the runtime. Provides access to per-type
/// options for the actor.
/// </summary>
public sealed class ActorRegistration
{
    /// <summary>
    /// Initializes a new instance of <see cref="ActorRegistration" />.
    /// </summary>
    /// <param name="type">The <see cref="ActorTypeInformation" /> for the actor type.</param>
    public ActorRegistration(ActorTypeInformation type) : this(type, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ActorRegistration" />.
    /// </summary>
    /// <param name="type">The <see cref="ActorTypeInformation" /> for the actor type.</param>
    /// <param name="options">The optional <see cref="ActorRuntimeOptions"/> that are specified for this type only.</param>
    public ActorRegistration(ActorTypeInformation type, ActorRuntimeOptions options)
    {
        this.Type = type;
        this.TypeOptions = options;
    }

    /// <summary>
    /// Gets the <see cref="ActorTypeInformation" /> for the actor type.
    /// </summary>
    public ActorTypeInformation Type { get; }

    /// <summary>
    /// Gets or sets the <see cref="ActorActivator" /> to use for the actor. If not set the default
    /// activator of the runtime will be used.
    /// </summary>
    public ActorActivator Activator { get; set; }

    /// <summary>
    /// An optional set of options for this specific actor type. These will override the top level or default values.
    /// </summary>
    public ActorRuntimeOptions TypeOptions { get; }
}