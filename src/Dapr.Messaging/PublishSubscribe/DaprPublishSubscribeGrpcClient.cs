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
internal sealed class DaprPublishSubscribeGrpcClient(
    P.DaprClient client,
    HttpClient httpClient,
    string? daprApiToken = null) : DaprPublishSubscribeClient(client, httpClient, daprApiToken)
{
    /// <summary>
    /// Dynamically subscribes to a Publish/Subscribe component and topic.
    /// </summary>
    /// <param name="pubSubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="messageHandler">The delegate reflecting the action to take upon messages received by the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public override async Task<IAsyncDisposable> SubscribeAsync(
        string pubSubName,
        string topicName,
        DaprSubscriptionOptions options,
        TopicMessageHandler messageHandler,
        CancellationToken cancellationToken = default)
    {
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, Client);
        await receiver.SubscribeAsync(cancellationToken);
        return receiver;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.HttpClient.Dispose();
        }
    }
}

