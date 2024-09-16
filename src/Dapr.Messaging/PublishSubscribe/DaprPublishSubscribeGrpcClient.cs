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

using P = Dapr.Client.Autogen.Grpc.v1.Dapr;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A client for interacting with the Dapr endpoints.
/// </summary>
internal sealed class DaprPublishSubscribeGrpcClient : DaprPublishSubscribeClient
{
    /// <summary>
    /// The various receiver clients created for each combination of Dapr pubsub component and topic name.
    /// </summary>
    private readonly Dictionary<(string, string), PublishSubscribeReceiver> clients = new();

    /// <summary>
    /// Maintains a single connection to the Dapr dynamic subscription endpoint.
    /// </summary>
    private readonly ConnectionManager connectionManager;

    /// <summary>
    /// Creates a new instance of a <see cref="DaprPublishSubscribeGrpcClient"/>
    /// </summary>
    public DaprPublishSubscribeGrpcClient(P.DaprClient client)
    {
        connectionManager = new(client);
    }

    /// <summary>
    /// Dynamically subscribes to a Publish/Subscribe component and topic.
    /// </summary>
    /// <param name="pubSubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="messageHandler">The delegate reflecting the action to take upon messages received by the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public override IAsyncDisposable Register(string pubSubName, string topicName, DaprSubscriptionOptions options, TopicMessageHandler messageHandler, CancellationToken cancellationToken)
    {
        var key = (pubSubName, topicName);
        if (clients.ContainsKey(key))
            throw new Exception(
                $"A subscription has already been created for Dapr pub/sub component '{pubSubName}' and topic '{topicName}'");

        clients[key] = new PublishSubscribeReceiver(pubSubName, topicName, options, connectionManager, messageHandler);
        return clients[key];
    }
}
