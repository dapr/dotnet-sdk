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

namespace Dapr.Actors.Next.Abstractions.Attributes;

/// <summary>
/// Marks a concrete class as a Dapr actor implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DaprActorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new attribute instance.
    /// </summary>
    public DaprActorAttribute()
    {
    }

    /// <summary>
    /// Initializes a new attribute instance with an explicit actor type name.
    /// </summary>
    public DaprActorAttribute(string actorType)
    {
        ActorType = actorType;
    }

    /// <summary>
    /// Gets the explicit actor type name, when supplied.
    /// </summary>
    public string? ActorType { get; }

    /// <summary>
    /// Gets or sets the actor contract version emitted into the generated registry.
    /// </summary>
    public int ContractVersion { get; set; } = 1;
}
