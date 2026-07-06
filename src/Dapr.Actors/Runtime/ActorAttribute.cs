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
/// Contains optional properties related to an actor implementation.
/// </summary>
/// <remarks>Intended to be attached to actor implementation types (i.e.those derived from <see cref="Actor" />).</remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ActorAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the actor type represented by the actor.
    /// </summary>
    /// <value>The <see cref="string"/> name of the actor type represented by the actor.</value>
    /// <remarks>If set, this value will override the default actor type name derived from the actor implementation type.</remarks>
    public string TypeName { get; set; }
}