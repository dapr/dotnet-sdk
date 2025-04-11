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

using System.Text.Json;
using Dapr.Common.Serialization;
using P = Dapr.Client.Autogen.Grpc.v1.Dapr;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A client for interacting with the Dapr endpoints.
/// </summary>
internal sealed class DaprPublishSubscribeGrpcClient(
    P.DaprClient client,
    HttpClient httpClient,
    JsonSerializerOptions jsonSerializerOptions,
    string? daprApiToken = null) : DaprPublishSubscribeClient(client, httpClient, jsonSerializerOptions, daprApiToken)
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

    /// <summary>
    /// Publishes an event to the specified topic.
    /// </summary>
    /// <param name="pubSubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to publish to.</param>
    /// <param name="data">The optional data that will be JSON serialized and provided as the event payload.</param>
    /// <param name="metadata">A collection of optional metadata key/value pairs that will be provided to the component. The valid
    /// metadata keys and values are determined by the type of PubSub component used.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel the operation.</param>
    /// <typeparam name="TData">The type of data that will be JSON serialized and provided  as the event payload.</typeparam>
    public override async Task PublishEventAsync<TData>(
        string pubSubName,
        string topicName,
        TData? data = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) where TData : class
    {
        ArgumentException.ThrowIfNullOrEmpty(pubSubName, nameof(pubSubName));
        ArgumentException.ThrowIfNullOrEmpty(topicName, nameof(topicName));

        var payload = data is null ? null : TypeConverters.ToJsonByteString(data, JsonSerializerOptions);
        return MakePublishRequestAsync(pubSubName, topicName, payload, metadata, data is CloudEvent  
    }

    /// <summary>
    /// Bulk-publishes multiple events to the specified topic at once.
    /// </summary>
    /// <param name="pubSubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to publish to.</param>
    /// <param name="data">The collection of data that will be JSON serialized and provided as the event payload.</param>
    /// <param name="metadata">A collection of optional metadata key/value pairs that will be provided to the component. The valid
    /// metadata keys and values are determined by the type of PubSub component used.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel the operation.</param>
    /// <typeparam name="TData">The type of data that will be JSON serialized and provided  as the event payload.</typeparam>
    public override async Task PublishEventAsync<TData>(
        string pubSubName,
        string topicName,
        IReadOnlyList<TData> data,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Publishes an event with a byte-based payload to the specified topic.
    /// </summary>
    /// <param name="pubSubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to publish to.</param>
    /// <param name="data">The raw byte data used as the event payload.</param>
    /// <param name="dataContentType">The content type of the given bytes. This defaults to "application/json".</param>
    /// <param name="metadata">A collection of optional metadata key/value pairs that will be provided to the component. The valid
    /// metadata keys and values are determined by the type of PubSub component used.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel the operation.</param>
    public override async Task PublishEventAsync(
        string pubSubName,
        string topicName,
        ReadOnlyMemory<byte> data,
        string dataContentType = "application/json",
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private async Task MakePublishRequestAsync()
    {
        
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

