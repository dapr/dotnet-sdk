// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// The base implementation of a Dapr pub/sub client.
/// </summary>
public abstract class DaprPublishSubscribeClient
{
    /// <summary>
    /// Dynamically subscribes to a Publish/Subscribe component and topic.
    /// </summary>
    /// <param name="pubsubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TopicMessage}"/> containing the various messages returned by the subscription.</returns>
    public abstract IAsyncEnumerable<TopicMessage> SubscribeAsync(string pubsubName, string topicName, DaprSubscriptionOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Used to acknowledge receipt of a message and indicate how the Dapr sidecar should handle it.
    /// </summary>
    /// <param name="pubsubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="messageId">The identifier of the message to apply the action to.</param>
    /// <param name="messageAction">Indicates the action to perform on the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract Task AcknowledgeMessageAsync(string pubsubName, string topicName, string messageId,
        TopicMessageAction messageAction, CancellationToken cancellationToken);

    /// <summary>
    /// Unsubscribes a streaming subscription for the specified Publish/Subscribe component and topic.
    /// </summary>
    /// <param name="pubsubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract Task UnsubscribeAsync(string pubsubName, string topicName, CancellationToken cancellationToken);
}
