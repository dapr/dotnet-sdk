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
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Dapr.Common.Serialization;

/// <summary>
/// JSON-based implementation of <see cref="IDaprSerializer"/> using System.Text.Json.
/// </summary>
public class JsonDaprSerializer : IDaprSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDaprSerializer"/> class with default JSON options.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="JsonSerializerDefaults.Web"/> which provides camelCase naming and other web-friendly defaults.
    /// Also enables <see cref="JsonSerializerOptions.IncludeFields"/> for compatibility with field-based types.
    /// </remarks>
    public JsonDaprSerializer() : this(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        IncludeFields = true // https://github.com/dapr/dotnet-sdk/issues/1757
    })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDaprSerializer"/> class with custom JSON options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use for all serialization operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
    public JsonDaprSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Serialize<T>(T value) =>
        value is null ? string.Empty : JsonSerializer.Serialize(value, GetTypeInfo<T>());

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization with a runtime Type requires dynamic code generation.")]
    public string Serialize(object? value, Type? inputType = null)
    {
        if (value is null)
            return string.Empty;

        return inputType is not null
            ? JsonSerializer.Serialize(value, inputType, _options)
            : JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string? data) => string.IsNullOrEmpty(data) ? default : JsonSerializer.Deserialize(data, GetTypeInfo<T>());

    /// <summary>
    /// Serializes a value of a known type directly to its UTF-8 JSON byte representation, avoiding an
    /// intermediate string allocation.
    /// </summary>
    /// <typeparam name="T">The compile-time type of the value being serialized.</typeparam>
    /// <param name="value">The value to serialize. Can be null.</param>
    /// <returns>
    /// The UTF-8 JSON bytes for the value. Returns an empty array if <paramref name="value"/> is null,
    /// matching the empty-string contract of <see cref="Serialize{T}"/>.
    /// </returns>
    public byte[] SerializeToUtf8Bytes<T>(T value) =>
        value is null ? Array.Empty<byte>() : JsonSerializer.SerializeToUtf8Bytes(value, GetTypeInfo<T>());

    /// <summary>
    /// Deserializes UTF-8 JSON bytes to an object of the specified type, avoiding an intermediate string
    /// allocation.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to.</typeparam>
    /// <param name="utf8Json">The UTF-8 JSON bytes to deserialize. Can be empty.</param>
    /// <returns>
    /// The deserialized object of type <typeparamref name="T"/>, or <c>default(T)</c> if
    /// <paramref name="utf8Json"/> is empty.
    /// </returns>
    public T? DeserializeFromUtf8Bytes<T>(ReadOnlySpan<byte> utf8Json) =>
        utf8Json.IsEmpty ? default : JsonSerializer.Deserialize(utf8Json, GetTypeInfo<T>());

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON deserialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON deserialization with a runtime Type requires dynamic code generation.")]
    public object? Deserialize(string? data, Type returnType)
    {
        ArgumentNullException.ThrowIfNull(returnType);

        return string.IsNullOrEmpty(data) ? null : JsonSerializer.Deserialize(data, returnType, _options);
    }

    private JsonTypeInfo<T> GetTypeInfo<T>()
    {
        if (_options.TypeInfoResolver is not null)
        {
            return (JsonTypeInfo<T>)_options.GetTypeInfo(typeof(T));
        }

        if (JsonSerializer.IsReflectionEnabledByDefault)
        {
            return (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T));
        }

        throw new NotSupportedException($"JsonTypeInfo metadata for '{typeof(T)}' was not provided.");
    }
}
