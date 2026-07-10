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

namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Serializes and deserializes closed actor state payload types for migration.
/// </summary>
public interface IActorStateMigrationSerializer
{
    /// <summary>
    /// Gets the serializer identity recorded in actor state headers.
    /// </summary>
    string SerializerId { get; }

    /// <summary>
    /// Gets the serializer version recorded in actor state headers.
    /// </summary>
    int SerializerVersion { get; }

    /// <summary>
    /// Deserializes bytes into a closed payload type.
    /// </summary>
    T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes);

    /// <summary>
    /// Serializes a closed payload type to bytes.
    /// </summary>
    byte[] SerializeToBytes<T>(T value);
}
