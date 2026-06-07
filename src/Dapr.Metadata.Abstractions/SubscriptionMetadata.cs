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
using Dapr.Common.JsonConverters;

namespace Dapr.Metadata.Abstractions;

/// <summary>
/// Represents the metadata for a registered Dapr subscription.
/// </summary>
public class SubscriptionMetadata
{
    /// <summary>
    /// The name of the pub/sub component.
    /// </summary>
    [JsonPropertyName("pubsubname")]
    public string? PubSubName { get; init; }
    
    /// <summary>
    /// The name of the subscription topic.
    /// </summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }
    
    /// <summary>
    /// Metadata associated with the subscription.
    /// </summary>
    [JsonPropertyName("metadata")]
    public object? Metadata { get; init; }
    
    /// <summary>
    /// The list of rules associated with the subscription.
    /// </summary>
    [JsonPropertyName("rules")]
    public IReadOnlyCollection<SubscriptionRuleMetadata> Rules { get; init; } = [];
    
    /// <summary>
    /// The dead letter topic name.
    /// </summary>
    [JsonPropertyName("deadLetterTopic")]
    public string? DeadLetterTopic { get; init; }
    
    /// <summary>
    /// The type of the subscription.
    /// </summary>
    [JsonConverter(typeof(GenericEnumJsonConverter<SubscriptionType>))]
    [JsonPropertyName("type")]
    public SubscriptionType Type { get; init; }
}
