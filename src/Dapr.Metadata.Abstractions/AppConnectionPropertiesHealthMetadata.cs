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
// ------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Dapr.Metadata.Abstractions;

/// <summary>
/// Represents the metadata for app connection properties health.
/// </summary>
public class AppConnectionPropertiesHealthMetadata
{
    /// <summary>
    /// The health check path applicable for HTTP protocol.
    /// </summary>
    [JsonPropertyName("healthCheckPath")]
    public string? HealthCheckPath { get; init; }
    
    /// <summary>
    /// The time between each health probe, in Go duration format.
    /// </summary>
    [JsonPropertyName("healthProbeInterval")]
    public string? HealthProbeInterval { get; init; }
    
    /// <summary>
    /// The timeout for each health probe, in Go duration format.
    /// </summary>
    [JsonPropertyName("healthProbeTimeout")]
    public string? HealthProbeTimeout { get; init; }
    
    /// <summary>
    /// The max number of failed health probes before the app is considered unhealthy.
    /// </summary>
    [JsonPropertyName("healthThreshold")]
    public int? HealthThreshold { get; init; }
}
