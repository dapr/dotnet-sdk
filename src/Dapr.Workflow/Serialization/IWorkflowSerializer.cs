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
//  ------------------------------------------------------------------------

using System;

namespace Dapr.Workflow.Serialization;

/// <summary>
/// Provides serialization and deserialization services for workflow data.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for converting objects to and from string representations
/// for transmission between workflows, activities, and the Dapr sidecar. The default implementation uses
/// System.Text.Json, but custom implementations can use any serialization format (Protobuf, MessagePack, XML, etc.).
/// </remarks>
public interface IWorkflowSerializer
{
    /// <summary>
    /// Serializes an object to a string representation.
    /// </summary>
    /// <param name="value">The object to serialize. Can be null.</param>
    /// <param name="inputType">
    /// Optional type hint for the object being serialized. Some serializers may use this for better type fidelity.
    /// If null, the serializer should use the runtime type of <paramref name="value"/>.
    /// </param>
    /// <returns>
    /// A string representation of the object. Returns an empty string if <paramref name="value"/> is null.
    /// </returns>
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
    /// <param name="data">The string data to deserialize. Can be null or empty.</param>
    /// <param name="returnType">The target type to deserialize to.</param>
    /// <returns>
    /// The deserialized object, or <c>null</c> if <paramref name="data"/> is null or empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="returnType"/> is null.</exception>
    object? Deserialize(string? data, Type returnType);
}
