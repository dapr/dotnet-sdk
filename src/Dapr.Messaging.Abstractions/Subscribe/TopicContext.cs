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
/// Context describing a single topic-message delivery, made available to <see cref="ITopicHandler{TMessage}"/>
/// implementations.
/// </summary>
public sealed class TopicContext
{
    /// <summary>
    /// The name of the Dapr publish/subscribe component the message was delivered from.
    /// </summary>
    public string PubsubName { get; init; }

    /// <summary>
    /// The name of the topic the message was delivered to.
    /// </summary>
    public string TopicName { get; init; }

    /// <summary>
    /// The unique identifier of the message.
    /// </summary>
    public string MessageId { get; init; }

    /// <summary>
    /// Metadata forwarded by the Dapr runtime with the delivery.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// CloudEvent envelope properties forwarded as headers when the message is a CloudEvent.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The raw payload of the message, when <c>EnableRawPayload</c> is enabled on the subscription.
    /// </summary>
    public ReadOnlyMemory<byte> RawPayload { get; init; }

    /// <summary>
    /// The parsed CloudEvent envelope, populated when the content type of the message is
    /// <c>application/cloudevents+json</c>.
    /// </summary>
    public CloudEvent CloudEvent { get; init; }
}
