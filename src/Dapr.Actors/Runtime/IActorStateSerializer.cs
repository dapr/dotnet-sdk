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
/// Represents a custom serializer for actor state.
/// </summary>
internal interface IActorStateSerializer
{
    /// <summary>
    /// Deserializes from the given byte array to <typeparamref name="T"/>.
    /// </summary>
    /// <param name="buffer">The byte array to deserialize from.</param>
    /// <returns>The deserialized value.</returns>
    T Deserialize<T>(byte[] buffer);

    /// <summary>
    /// Serializes an object into a byte array.
    /// </summary>
    /// <param name="stateType">Type of the state.</param>
    /// <param name="state">The state value to serialize.</param>
    byte[] Serialize<T>(Type stateType, T state);
}