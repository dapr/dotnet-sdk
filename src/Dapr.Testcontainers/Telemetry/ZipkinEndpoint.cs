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

using System.Text.Json.Serialization;

namespace Dapr.Testcontainers.Telemetry;

/// <summary>
/// Endpoint metadata attached to a Zipkin span.
/// </summary>
public sealed class ZipkinEndpoint
{
    /// <summary>
    /// Service name reported by the emitter.
    /// </summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>
    /// IPv4 address reported by the emitter.
    /// </summary>
    [JsonPropertyName("ipv4")]
    public string? Ipv4 { get; init; }

    /// <summary>
    /// IPv6 address reported by the emitter.
    /// </summary>
    [JsonPropertyName("ipv6")]
    public string? Ipv6 { get; init; }

    /// <summary>
    /// Port reported by the emitter.
    /// </summary>
    [JsonPropertyName("port")]
    public int? Port { get; init; }
}
