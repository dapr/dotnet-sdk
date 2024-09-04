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
    private readonly PublishSubscribeReceiverBuilder _builder;

    private readonly Dictionary<(string, string), PublishSubscribeReceiver> _clients =
        new Dictionary<(string, string), PublishSubscribeReceiver>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    public DaprPublishSubscribeGrpcClient(PublishSubscribeReceiverBuilder builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pubsubName"></param>
    /// <param name="topicName"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override IAsyncEnumerable<TopicMessage> SubscribeAsync(string pubsubName, string topicName, DaprSubscriptionOptions options,
        CancellationToken cancellationToken)
    {
        var receiver = _builder.Build(pubsubName, topicName, options);
        _clients[(pubsubName, topicName)] = receiver;

        return receiver.SubscribeAsync(cancellationToken);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pubsubName"></param>
    /// <param name="topicName"></param>
    /// <param name="messageId"></param>
    /// <param name="messageAction"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task AcknowledgeMessageAsync(string pubsubName, string topicName, string messageId,
        TopicMessageAction messageAction, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue((pubsubName, topicName), out var receiver))
        {
            throw new Exception($"Unable to find receiver instance for specified publish/subscribe component name '{pubsubName}' and topic '{topicName}'.");
        }

        await receiver.AcknowledgeMessageAsync(messageId, messageAction, cancellationToken);
    }

    public override async Task UnsubscribeAsync(string pubsubName, string topicName, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue((pubsubName, topicName), out var receiver))
        {
            throw new Exception($"Unable to find receiver instance for specified publish/subscribe component name '{pubsubName}' and topic '{topicName}'.");
        }

        await receiver.DisposeAsync();
    }
}
