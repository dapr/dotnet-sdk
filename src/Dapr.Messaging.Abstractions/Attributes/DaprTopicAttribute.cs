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

namespace Dapr.Messaging;

/// <summary>
/// Identifies a class as a Dapr pub/sub topic handler for the given component and topic.
/// Apply the attribute (multiple times, if the handler serves several topics) to a class that
/// implements <see cref="ITopicHandler{TMessage}"/> (or <see cref="ITopicHandler{TMessage, TResult}"/>).
/// The <c>Dapr.Messaging.Generators</c> source generator discovers these at compile time and emits
/// the dispatchers, subscriber registry, subscription manifest, and <c>JsonSerializerContext</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DaprTopicAttribute : Attribute
{
    /// <summary>
    /// Creates a new <see cref="DaprTopicAttribute"/>.
    /// </summary>
    /// <param name="pubsubName">The name of the Dapr publish/subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    public DaprTopicAttribute(string pubsubName, string topicName)
    {
        this.PubsubName = pubsubName;
        this.TopicName = topicName;
    }

    /// <summary>The name of the Dapr publish/subscribe component.</summary>
    public string PubsubName { get; }

    /// <summary>The name of the topic.</summary>
    public string TopicName { get; }

    /// <summary>
    /// The application route used for HTTP delivery. When omitted, the topic name is used.
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// The delivery mode for the subscription. Defaults to <see cref="DeliveryMode.Streaming"/>.
    /// </summary>
    public DeliveryMode Delivery { get; set; } = DeliveryMode.Streaming;

    /// <summary>The CEL routing-rule match expression, if any.</summary>
    public new string? Match { get; set; }

    /// <summary>The priority of the routing rule (required when <see cref="Match"/> is set).</summary>
    public int Priority { get; set; }

    /// <summary>The dead-letter topic, if any.</summary>
    public string? DeadLetterTopic { get; set; }

    /// <summary>Whether the raw payload should be forwarded to the handler.</summary>
    public bool EnableRawPayload { get; set; }

    /// <summary>Whether bulk subscribe is enabled for the subscription.</summary>
    public bool BulkSubscribe { get; set; }

    /// <summary>The maximum number of messages in a bulk request (used when <see cref="BulkSubscribe"/> is true).</summary>
    public int MaxMessagesCount { get; set; } = 100;

    /// <summary>The maximum await time, in milliseconds, for assembling a bulk request.</summary>
    public int MaxAwaitDurationMs { get; set; } = 1000;

    /// <summary>
    /// Optional metadata keys, used to correlate <see cref="DaprTopicMetadataAttribute"/> entries
    /// with this topic. When <c>null</c>, metadata attributes apply to all topics on the class.
    /// </summary>
    public string[]? MetadataKeys { get; set; }
}
