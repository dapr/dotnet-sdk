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
/// Represents the metadata retrieved from the Dapr runtime.
/// </summary>
public class DaprMetadata
{
    /// <summary>
    /// Application ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? AppId { get; init; }
    
    /// <summary>
    /// Version of the Dapr runtime.
    /// </summary>
    [JsonPropertyName("runtimeVersion")]
    public string? RuntimeVersion { get; init; }

    /// <summary>
    /// List of named features enabled by Dapr configuration.
    /// </summary>
    [JsonPropertyName("enabledFeatures")]
    public IReadOnlyCollection<string> EnabledFeatures { get; init; } = [];

    /// <summary>
    /// Metadata for the registered actors.
    /// </summary>
    public IReadOnlyCollection<RegisteredActorMetadata> Actors { get; init; } = [];

    /// <summary>
    /// A collection of custom attributes as key/value pairs. 
    /// </summary>
    [JsonPropertyName("extended")]
    public Dictionary<string, string> CustomAttributes { get; init; } = [];

    /// <summary>
    /// Loaded component metadata.
    /// </summary>
    [JsonPropertyName("components")]
    public IReadOnlyCollection<ComponentMetadata> Components { get; init; } = [];
    
    /// <summary>
    /// HTTP endpoint metadata.
    /// </summary>
    [JsonPropertyName("httpEndpoints")]
    public IReadOnlyCollection<HttpEndpointMetadata> HttpEndpoints { get; init; } = [];
    
    /// <summary>
    /// PubSub subscription metadata.
    /// </summary>
    [JsonPropertyName("subscriptions")]
    public IReadOnlyCollection<SubscriptionMetadata> Subscriptions { get; init; } = [];

    /// <summary>
    /// App connection properties.
    /// </summary>
    [JsonPropertyName("appConnectionProperties")]
    public AppConnectionPropertyMetadata AppConnectionProperties { get; init; } = new();

    /// <summary>
    /// Scheduler connection metadata properties.
    /// </summary>
    [JsonPropertyName("scheduler")]
    public SchedulerMetadata SchedulerMetadata { get; init; } = new();

    /// <summary>
    /// Workflow runtime metadata properties.
    /// </summary>
    [JsonPropertyName("workflows")]
    public WorkflowMetadata Workflows { get; init; } = new();
}
