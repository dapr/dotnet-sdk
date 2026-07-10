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

using Dapr.Actors.Next.Abstractions.State.Versioning;

namespace Dapr.Actors.Next.Core.Serialization;

/// <summary>
/// Serializes actor payloads at the runtime wire boundary.
/// </summary>
public interface IActorWireSerializer : IActorStateMigrationSerializer
{
    /// <inheritdoc />
    string IActorStateMigrationSerializer.SerializerId => "dapr-json";

    /// <inheritdoc />
    int IActorStateMigrationSerializer.SerializerVersion => 1;

    /// <summary>
    /// Converts JSON text to UTF-8 wire bytes.
    /// </summary>
    byte[] JsonToBytes(string? json);

    /// <summary>
    /// Converts UTF-8 wire bytes to JSON text.
    /// </summary>
    string? BytesToJson(ReadOnlyMemory<byte> bytes);

    /// <summary>
    /// Serializes a value to UTF-8 wire bytes.
    /// </summary>
    new byte[] SerializeToBytes<T>(T value);

    /// <summary>
    /// Deserializes a value from UTF-8 wire bytes.
    /// </summary>
    new T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes);
}
