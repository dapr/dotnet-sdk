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
/// 
/// </summary>
public sealed class DaprPublishSubscribeGrpcClient : DaprPublishSubscribeClient
{
    /// <summary>
    /// Maintains a reference to the receiver builder factory.
    /// </summary>
    private readonly PublishSubscribeReceiverBuilder _builder;
    /// <summary>
    /// The various receiver clients created for each combination of Dapr pubsub component and topic name.
    /// </summary>
    private readonly Dictionary<(string, string), PublishSubscribeReceiver> _clients =
        new Dictionary<(string, string), PublishSubscribeReceiver>();

    /// <summary>
    /// Creates a new instance of a <see cref="DaprPublishSubscribeGrpcClient"/>
    /// </summary>
    public DaprPublishSubscribeGrpcClient(PublishSubscribeReceiverBuilder builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// Dynamically subscribes to a Publish/Subscribe component and topic.
    /// </summary>
    /// <param name="pubsubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TopicMessage}"/> containing the various messages returned by the subscription.</returns>
    public override IAsyncEnumerable<TopicMessage> SubscribeAsync(string pubsubName, string topicName, DaprSubscriptionOptions options,
        CancellationToken cancellationToken)
    {
        var receiver = _builder.Build(pubsubName, topicName, options);
        _clients[(pubsubName, topicName)] = receiver;

        return receiver.SubscribeAsync(cancellationToken);
    }

    /// <summary>
    /// Used to acknowledge receipt of a message and indicate how the Dapr sidecar should handle it.
    /// </summary>
    /// <param name="pubsubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="messageId">The identifier of the message to apply the action to.</param>
    /// <param name="messageAction">Indicates the action to perform on the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public override async Task AcknowledgeMessageAsync(string pubsubName, string topicName, string messageId,
        TopicMessageAction messageAction, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue((pubsubName, topicName), out var receiver))
        {
            throw new Exception($"Unable to find receiver instance for specified publish/subscribe component name '{pubsubName}' and topic '{topicName}'.");
        }

        await receiver.AcknowledgeMessageAsync(messageId, messageAction, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes a streaming subscription for the specified Publish/Subscribe component and topic.
    /// </summary>
    /// <param name="pubsubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public override async Task UnsubscribeAsync(string pubsubName, string topicName, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue((pubsubName, topicName), out var receiver))
        {
            throw new Exception($"Unable to find receiver instance for specified publish/subscribe component name '{pubsubName}' and topic '{topicName}'.");
        }

        await receiver.DisposeAsync();
    }
}
