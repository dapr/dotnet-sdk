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

#nullable disable
namespace Dapr.Messaging;

/// <summary>
/// A compile-time descriptor for a topic subscription, produced by the source generator from
/// <c>[DaprTopic]</c>-annotated <see cref="ITopicHandler{TMessage}"/> implementations.
/// </summary>
public sealed class TopicSubscriptionDescriptor
{
    /// <summary>The name of the Dapr publish/subscribe component.</summary>
    public string PubsubName { get; set; }

    /// <summary>The name of the topic.</summary>
    public string TopicName { get; set; }

    /// <summary>The application route used for HTTP delivery.</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>The handler type that handles messages on this subscription.</summary>
    public Type HandlerType { get; set; }

    /// <summary>The deserialized message type handled by <see cref="HandlerType"/>.</summary>
    public Type MessageType { get; set; }

    /// <summary>The delivery mode for the subscription.</summary>
    public DeliveryMode Delivery { get; set; }

    /// <summary>The CEL routing-rule match expression, if any.</summary>
    public string Match { get; set; }

    /// <summary>The priority of the routing rule, if applicable.</summary>
    public int? Priority { get; set; }

    /// <summary>The dead-letter topic, if configured.</summary>
    public string DeadLetterTopic { get; set; }

    /// <summary>Whether the raw payload should be forwarded to the handler.</summary>
    public bool? EnableRawPayload { get; set; }

    /// <summary>Bulk subscribe configuration, if enabled.</summary>
    public BulkSubscribeOptions BulkSubscribe { get; set; }

    /// <summary>Metadata associated with the subscription.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
