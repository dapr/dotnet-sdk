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

using System.Text.Json;

namespace Dapr.Common.Serialization;

/// <summary>
/// JSON-based implementation of <see cref="IDaprSerializer"/> using System.Text.Json.
/// </summary>
/// <remarks>
/// This is the default serializer used across all Dapr SDK components. It provides
/// consistent JSON serialization behavior using <see cref="JsonSerializerDefaults.Web"/>
/// (camelCase naming and case-insensitive deserialization) by default.
/// </remarks>
public sealed class JsonDaprSerializer : IDaprSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDaprSerializer"/> class with default JSON options.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="JsonSerializerDefaults.Web"/> which provides camelCase naming
    /// and other web-friendly defaults, with <see cref="JsonSerializerOptions.IncludeFields"/>
    /// enabled for field serialization support.
    /// </remarks>
    public JsonDaprSerializer()
        : this(new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDaprSerializer"/> class with custom JSON options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use for all serialization operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is <see langword="null"/>.</exception>
    public JsonDaprSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public string Serialize(object? value, Type? inputType = null)
    {
        if (value is null)
            return string.Empty;

        return inputType is not null
            ? JsonSerializer.Serialize(value, inputType, _options)
            : JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc />
    public byte[] SerializeToBytes(object? value, Type? inputType = null)
    {
        if (value is null)
            return [];

        return inputType is not null
            ? JsonSerializer.SerializeToUtf8Bytes(value, inputType, _options)
            : JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string? data) =>
        string.IsNullOrEmpty(data) ? default : JsonSerializer.Deserialize<T>(data, _options);

    /// <inheritdoc />
    public object? Deserialize(string? data, Type returnType)
    {
        ArgumentNullException.ThrowIfNull(returnType);
        return string.IsNullOrEmpty(data) ? null : JsonSerializer.Deserialize(data, returnType, _options);
    }

    /// <inheritdoc />
    public T? DeserializeFromBytes<T>(ReadOnlySpan<byte> data) =>
        data.IsEmpty ? default : JsonSerializer.Deserialize<T>(data, _options);

    /// <inheritdoc />
    public object? DeserializeFromBytes(ReadOnlySpan<byte> data, Type returnType)
    {
        ArgumentNullException.ThrowIfNull(returnType);
        return data.IsEmpty ? null : JsonSerializer.Deserialize(data, returnType, _options);
    }
}
