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

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Marks a domain event type as routable by the default <see cref="AttributeOutboxMessageFactory"/>.
/// The attribute supplies the Dapr pub/sub component name and topic that the event will be
/// published to when discovered by the <c>DaprOutboxSaveChangesInterceptor</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class DaprOutboxEventAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprOutboxEventAttribute"/> class.
    /// </summary>
    /// <param name="pubSubName">The name of the Dapr pub/sub component to publish the event to.</param>
    /// <param name="topic">The topic to publish the event to.</param>
    public DaprOutboxEventAttribute(string pubSubName, string topic)
    {
        ArgumentException.ThrowIfNullOrEmpty(pubSubName);
        ArgumentException.ThrowIfNullOrEmpty(topic);
        PubSubName = pubSubName;
        Topic = topic;
    }

    /// <summary>
    /// The name of the Dapr pub/sub component the event will be published to.
    /// </summary>
    public string PubSubName { get; }

    /// <summary>
    /// The topic on the pub/sub component the event will be published to.
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// Optional override for the CloudEvent <c>source</c> field.
    /// </summary>
    public string? CloudEventSource { get; init; }

    /// <summary>
    /// Optional override for the CloudEvent <c>type</c> field. When unset, the CLR type
    /// name of the domain event is used.
    /// </summary>
    public string? CloudEventType { get; init; }
}
