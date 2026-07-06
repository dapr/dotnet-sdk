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
/// Represents the metadata of an application connection property.
/// </summary>
public class AppConnectionPropertyMetadata
{
    /// <summary>
    /// The port on which the app is listening.
    /// </summary>
    [JsonPropertyName("port")]
    public int? Port { get; init; }
    
    /// <summary>
    /// The protocol used by the app.
    /// </summary>
    [JsonPropertyName("protocol")]
    public string? Protocol { get; init; }
    
    /// <summary>
    /// The host address on whcih the app is listening.
    /// </summary>
    [JsonPropertyName("channelAddress")]
    public string? ChannelAddress { get; init; }
    
    /// <summary>
    /// The maximum number of concurrent requests the app can handle.
    /// </summary>
    [JsonPropertyName("maxConcurrency")]
    public int? MaxConcurrency { get; init; }
    
    /// <summary>
    /// The health check details of the app.
    /// </summary>
    [JsonPropertyName("health")]
    public AppConnectionPropertiesHealthMetadata? Health { get; init; }
}
