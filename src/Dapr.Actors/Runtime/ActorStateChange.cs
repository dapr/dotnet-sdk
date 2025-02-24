// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

#nullable enable
namespace Dapr.Actors.Runtime;

using System;

/// <summary>
/// Represents a change to an actor state with a given state name.
/// </summary>
/// <param name="StateName">The name of the actor state.</param>
/// <param name="Type">The type of value associated with the given actor state name.</param>
/// <param name="Value">The value associated with the given actor state name.</param>
/// <param name="ChangeKind">The kind of state change for the given actor state name.</param>
/// <param name="TTLExpireTime">The time to live for the state. If null, the state wil not expire.</param>
public sealed record ActorStateChange(
    string StateName,
    Type Type,
    object? Value,
    StateChangeKind ChangeKind,
    DateTimeOffset? TTLExpireTime);
