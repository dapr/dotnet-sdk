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

namespace Dapr.Client;

using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

/// <summary>
/// Some type converters.
/// </summary>
internal static class TypeConverters
{
    /// <summary>
    /// Converts an arbitrary type to a <see cref="System.Text.Json"/> based <see cref="ByteString"/>.
    /// </summary>
    /// <param name="data">The data to convert.</param>
    /// <param name="options">The JSON serialization options.</param>
    /// <typeparam name="T">The type of the given data.</typeparam>
    /// <returns>The given data as JSON based byte string.</returns>
    public static ByteString ToJsonByteString<T>(T data, JsonSerializerOptions options)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
        return ByteString.CopyFrom(bytes);
    }

    public static Any ToJsonAny<T>(T data, JsonSerializerOptions options)
    {
        return new Any() 
        { 
            Value = ToJsonByteString<T>(data, options),

            // This isn't really compliant protobuf, because we're not setting TypeUrl, but it's
            // what Dapr understands.
        };
    }

    public static T FromJsonByteString<T>(ByteString bytes, JsonSerializerOptions options)
    {
        return bytes.Length == 0 ? default : JsonSerializer.Deserialize<T>(bytes.Span, options);
    }

    public static T FromJsonAny<T>(Any any, JsonSerializerOptions options)
    {
        return FromJsonByteString<T>(any.Value, options);
    }
}
