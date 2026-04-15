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

namespace Dapr.Common.Serialization;

/// <summary>
/// Provides serialization and deserialization services for Dapr SDK components.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of this interface are responsible for converting objects to and from
/// byte representations for transmission between Dapr building blocks and the Dapr sidecar.
/// </para>
/// <para>
/// The default implementation uses System.Text.Json, but custom implementations can use
/// any serialization format (Protobuf, MessagePack, XML, etc.). A single implementation
/// can be shared across all Dapr clients (Actors, Workflows, Pub/Sub, etc.) to ensure
/// consistent serialization behavior.
/// </para>
/// </remarks>
public interface IDaprSerializer
{
    /// <summary>
    /// Serializes an object to its string representation.
    /// </summary>
    /// <param name="value">The object to serialize. Can be <see langword="null"/>.</param>
    /// <param name="inputType">
    /// Optional type hint for the object being serialized. Some serializers may use this
    /// for better type fidelity. If <see langword="null"/>, the serializer should use
    /// the runtime type of <paramref name="value"/>.
    /// </param>
    /// <returns>
    /// A string representation of the object, or an empty string if
    /// <paramref name="value"/> is <see langword="null"/>.
    /// </returns>
    string Serialize(object? value, Type? inputType = null);

    /// <summary>
    /// Serializes an object to its byte representation.
    /// </summary>
    /// <param name="value">The object to serialize. Can be <see langword="null"/>.</param>
    /// <param name="inputType">
    /// Optional type hint for the object being serialized. Some serializers may use this
    /// for better type fidelity. If <see langword="null"/>, the serializer should use
    /// the runtime type of <paramref name="value"/>.
    /// </param>
    /// <returns>
    /// A byte array representation of the object, or an empty array if
    /// <paramref name="value"/> is <see langword="null"/>.
    /// </returns>
    byte[] SerializeToBytes(object? value, Type? inputType = null);

    /// <summary>
    /// Deserializes a string to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to.</typeparam>
    /// <param name="data">The string data to deserialize. Can be <see langword="null"/> or empty.</param>
    /// <returns>
    /// The deserialized object of type <typeparamref name="T"/>, or
    /// <c>default(T)</c> if <paramref name="data"/> is <see langword="null"/> or empty.
    /// </returns>
    T? Deserialize<T>(string? data);

    /// <summary>
    /// Deserializes a string to an object of the specified type.
    /// </summary>
    /// <param name="data">The string data to deserialize. Can be <see langword="null"/> or empty.</param>
    /// <param name="returnType">The target type to deserialize to.</param>
    /// <returns>
    /// The deserialized object, or <see langword="null"/> if <paramref name="data"/>
    /// is <see langword="null"/> or empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="returnType"/> is <see langword="null"/>.
    /// </exception>
    object? Deserialize(string? data, Type returnType);

    /// <summary>
    /// Deserializes a byte array to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to.</typeparam>
    /// <param name="data">The byte data to deserialize. Can be <see langword="null"/> or empty.</param>
    /// <returns>
    /// The deserialized object of type <typeparamref name="T"/>, or
    /// <c>default(T)</c> if <paramref name="data"/> is <see langword="null"/> or empty.
    /// </returns>
    T? DeserializeFromBytes<T>(ReadOnlySpan<byte> data);

    /// <summary>
    /// Deserializes a byte array to an object of the specified type.
    /// </summary>
    /// <param name="data">The byte data to deserialize. Can be <see langword="null"/> or empty.</param>
    /// <param name="returnType">The target type to deserialize to.</param>
    /// <returns>
    /// The deserialized object, or <see langword="null"/> if <paramref name="data"/>
    /// is <see langword="null"/> or empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="returnType"/> is <see langword="null"/>.
    /// </exception>
    object? DeserializeFromBytes(ReadOnlySpan<byte> data, Type returnType);
}
