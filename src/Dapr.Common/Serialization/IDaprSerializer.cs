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
//  ------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace Dapr.Common.Serialization;

/// <summary>
/// Provides serialization and deserialization services for Dapr data payloads.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for converting objects to and from string representations.
/// The default implementation uses System.Text.Json, but custom implementations can use any serialization
/// format (Protobuf, MessagePack, XML, etc.).
/// </remarks>
public interface IDaprSerializer
{
    /// <summary>
    /// Serializes a value of a known type to its string representation.
    /// </summary>
    /// <typeparam name="T">The compile-time type of the value being serialized.</typeparam>
    /// <param name="value">The value to serialize. Can be null.</param>
    /// <returns>
    /// A string representation of the value. Returns an empty string if <paramref name="value"/> is null.
    /// </returns>
    string Serialize<T>(T value);

    /// <summary>
    /// Serializes an object to a string representation using an optional type hint.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="Serialize{T}"/> when the type is known at compile time.
    /// </remarks>
    /// <param name="value">The object to serialize. Can be null.</param>
    /// <param name="inputType">
    /// Optional type hint for the object being serialized. Some serializers may use this for better type fidelity.
    /// If null, the serializer should use the runtime type of <paramref name="value"/>.
    /// </param>
    /// <returns>
    /// A string representation of the object. Returns an empty string if <paramref name="value"/> is null.
    /// </returns>
    [RequiresUnreferencedCode("JSON serialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization with a runtime Type requires dynamic code generation.")]
    string Serialize(object? value, Type? inputType = null);

    /// <summary>
    /// Deserializes a string to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to.</typeparam>
    /// <param name="data">The string data to deserialize. Can be null or empty.</param>
    /// <returns>
    /// The deserialized object of type <typeparamref name="T"/>, or <c>default(T)</c> if <paramref name="data"/> is null or empty.
    /// </returns>
    T? Deserialize<T>(string? data);

    /// <summary>
    /// Deserializes a string to an object of the specified type.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="Deserialize{T}"/> when the type is known at compile time.
    /// </remarks>
    /// <param name="data">The string data to deserialize. Can be null or empty.</param>
    /// <param name="returnType">The target type to deserialize to.</param>
    /// <returns>
    /// The deserialized object, or <c>null</c> if <paramref name="data"/> is null or empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="returnType"/> is null.</exception>
    [RequiresUnreferencedCode("JSON deserialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON deserialization with a runtime Type requires dynamic code generation.")]
    object? Deserialize(string? data, Type returnType);
}
