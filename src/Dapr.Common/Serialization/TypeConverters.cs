// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Text.Json;
using Google.Protobuf;

namespace Dapr.Common.Serialization;

/// <summary>
/// Converters used to serialize and deserialize data.
/// </summary>
internal static class TypeConverters
{
    /// <summary>
    /// Converts an arbitrary type to a <see cref="System.Text.Json"/>-based <see cref="ByteString"/>. 
    /// </summary>
    /// <param name="data">The data to convert.</param>
    /// <param name="options">The JSON serialization options.</param>
    /// <typeparam name="T">The type of the given data.</typeparam>
    /// <returns>The given data as a JSON-based byte string.</returns>
    internal static ByteString ToJsonByteString<T>(T data, JsonSerializerOptions options)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
        return ByteString.CopyFrom(bytes);
    }

    /// <summary>
    /// Deserializes a <see cref="System.Text.Json"/>-based <see cref="ByteString"/> to an arbitrary type. 
    /// </summary>
    /// <param name="data">The data to convert.</param>
    /// <param name="options">The JSON serialization options.</param>
    /// <typeparam name="T">The type of the data to deserialize to.</typeparam>
    /// <returns>The strongly-typed deserialized data.</returns>
    internal static T? FromJsonByteString<T>(ByteString data, JsonSerializerOptions options) where T : class
    {
        return data.Length == 0 ? null : JsonSerializer.Deserialize<T>(data.Span, options);
    }
}
