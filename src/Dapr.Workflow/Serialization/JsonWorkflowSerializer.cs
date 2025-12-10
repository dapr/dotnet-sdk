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
using System.Text.Json;

namespace Dapr.Workflow.Serialization;

/// <summary>
/// JSON-based implementation of <see cref="IWorkflowSerializer"/> using System.Text.Json.
/// </summary>
public sealed class JsonWorkflowSerializer : IWorkflowSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWorkflowSerializer"/> class with default JSON options.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="JsonSerializerDefaults.Web"/> which provides camelCase naming and other web-friendly defaults.
    /// </remarks>
    public JsonWorkflowSerializer() : this(new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWorkflowSerializer"/> class with custom JSON options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use for all serialization operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
    public JsonWorkflowSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    
    /// <inheritdoc />
    public string Serialize(object? value, Type? inputType = null)
    {
        if (value is null)
            return string.Empty;

        // Use provided type hint for better serialization fidelity
        return inputType is not null 
            ? JsonSerializer.Serialize(value, inputType, _options) 
            : JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return default;

        return JsonSerializer.Deserialize<T>(data, _options);
    }

    /// <inheritdoc />
    public object? Deserialize(string? data, Type returnType)
    {
        ArgumentNullException.ThrowIfNull(returnType);
        
        if (string.IsNullOrEmpty(data))
            return null;

        return JsonSerializer.Deserialize(data, returnType, _options);
    }
}
