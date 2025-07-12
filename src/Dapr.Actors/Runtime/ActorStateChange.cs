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
/// Represents a change to an actor state with a given state name.
/// </summary>
public sealed class ActorStateChange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorStateChange"/> class.
    /// </summary>
    /// <param name="stateName">The name of the actor state.</param>
    /// <param name="type">The type of value associated with given actor state name.</param>
    /// <param name="value">The value associated with given actor state name.</param>
    /// <param name="changeKind">The kind of state change for given actor state name.</param>
    /// <param name="ttlExpireTime">The time to live for the state.</param>
    public ActorStateChange(string stateName, Type type, object value, StateChangeKind changeKind, DateTimeOffset? ttlExpireTime)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        this.StateName = stateName;
        this.Type = type;
        this.Value = value;
        this.ChangeKind = changeKind;
        this.TTLExpireTime = ttlExpireTime;
    }

    /// <summary>
    /// Gets the name of the actor state.
    /// </summary>
    /// <value>
    /// The name of the actor state.
    /// </value>
    public string StateName { get; }

    /// <summary>
    /// Gets the type of value associated with given actor state name.
    /// </summary>
    /// <value>
    /// The type of value associated with given actor state name.
    /// </value>
    public Type Type { get; }

    /// <summary>
    /// Gets the value associated with given actor state name.
    /// </summary>
    /// <value>
    /// The value associated with given actor state name.
    /// </value>
    public object Value { get; }

    /// <summary>
    /// Gets the kind of state change for given actor state name.
    /// </summary>
    /// <value>
    /// The kind of state change for given actor state name.
    /// </value>
    public StateChangeKind ChangeKind { get; }

    /// <summary>
    /// Gets the time to live for the state.
    /// </summary>
    /// <value>
    /// The time to live for the state.
    /// </value>
    /// <remarks>
    /// If null, the state will not expire.
    /// </remarks>
    public DateTimeOffset? TTLExpireTime { get; }
}