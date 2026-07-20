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

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Dapr.Testcontainers.Telemetry;

/// <summary>
/// Zipkin v2 span captured from Dapr runtime trace export.
/// </summary>
public sealed class ZipkinSpan
{
    /// <summary>
    /// Trace identifier.
    /// </summary>
    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    /// <summary>
    /// Span identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Parent span identifier, if any.
    /// </summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; init; }

    /// <summary>
    /// Span name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Span kind.
    /// </summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    /// <summary>
    /// Start timestamp in microseconds since Unix epoch.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    /// <summary>
    /// Duration in microseconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public long? Duration { get; init; }

    /// <summary>
    /// Local endpoint metadata.
    /// </summary>
    [JsonPropertyName("localEndpoint")]
    public ZipkinEndpoint? LocalEndpoint { get; init; }

    /// <summary>
    /// Remote endpoint metadata.
    /// </summary>
    [JsonPropertyName("remoteEndpoint")]
    public ZipkinEndpoint? RemoteEndpoint { get; init; }

    /// <summary>
    /// Span tags.
    /// </summary>
    [JsonPropertyName("tags")]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}
