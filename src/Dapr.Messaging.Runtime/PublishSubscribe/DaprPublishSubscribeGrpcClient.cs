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
using Dapr.Common;
using Google.Protobuf;
using Grpc.Core;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A client for interacting with the Dapr endpoints.
/// </summary>
internal sealed class DaprPublishSubscribeGrpcClient(
    P.Dapr.DaprClient client,
    HttpClient httpClient,
    JsonSerializerOptions jsonSerializerOptions,
    IDaprRuntimeCapabilities runtimeCapabilities,
    string? daprApiToken = null) : DaprPublishSubscribeClient(client, httpClient, jsonSerializerOptions, daprApiToken)
{
    private readonly IDaprRuntimeCapabilities runtimeCapabilities = runtimeCapabilities;
    private readonly VersionAwareDaprClient versionAwareClient = new(client, runtimeCapabilities);

    internal DaprPublishSubscribeGrpcClient(
        P.Dapr.DaprClient client,
        HttpClient httpClient,
        IDaprRuntimeCapabilities runtimeCapabilities,
        string? daprApiToken = null)
        : this(client, httpClient, new JsonSerializerOptions(), runtimeCapabilities, daprApiToken)
    {
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

    // -----------------------------------------------------------------------
    //  Publish
    // -----------------------------------------------------------------------

    public override Task PublishEventAsync<TData>(
        string pubsubName, string topicName, TData data, CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
        ArgumentVerifier.ThrowIfNull(data, nameof(data));

        var content = ToJsonByteString(data);
        var contentType = data is CloudEvent ? MessagingConstants.ContentTypeCloudEvent : null;
        return MakePublishRequest(pubsubName, topicName, content, metadata: null, contentType, cancellationToken);
    }

    public override Task PublishEventAsync<TData>(
        string pubsubName, string topicName, TData data, PublishOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
        ArgumentVerifier.ThrowIfNull(data, nameof(data));
        ArgumentVerifier.ThrowIfNull(options, nameof(options));

        var content = ToJsonByteString(data);
        var metadata = BuildPublishMetadata(options, data);
        var contentType = options.ContentType ?? (data is CloudEvent ? MessagingConstants.ContentTypeCloudEvent : null);
        return MakePublishRequest(pubsubName, topicName, content, metadata, contentType, cancellationToken);
    }

    public override Task PublishEventAsync(
        string pubsubName, string topicName, CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
        return MakePublishRequest(pubsubName, topicName, content: null, metadata: null, contentType: null, cancellationToken);
    }

    public override Task PublishByteEventAsync(
        string pubsubName,
        string topicName,
        ReadOnlyMemory<byte> data,
        string dataContentType = MessagingConstants.ContentTypeApplicationJson,
        PublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));

        var content = ByteString.CopyFrom(data.Span);
        var metadata = options is null ? null : BuildPublishMetadata(options, data: null);
        var contentType = options?.ContentType ?? dataContentType;
        return MakePublishRequest(pubsubName, topicName, content, metadata, contentType, cancellationToken);
    }

    public override async Task<BulkPublishResponse<TValue>> BulkPublishEventAsync<TValue>(
        string pubsubName,
        string topicName,
        IReadOnlyList<TValue> events,
        PublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
        ArgumentVerifier.ThrowIfNull(events, nameof(events));

        var envelope = new P.BulkPublishRequest { PubsubName = pubsubName, Topic = topicName };
        var entryMap = new Dictionary<string, BulkPublishEntry<TValue>>();
        var requestMetadata = options?.Metadata;

        for (var counter = 0; counter < events.Count; counter++)
        {
            var entry = new P.BulkPublishRequestEntry { EntryId = counter.ToString() };

            if (events[counter] is byte[] bytes)
            {
                entry.Event = ByteString.CopyFrom(bytes);
                entry.ContentType = MessagingConstants.ContentTypeApplicationOctetStream;
            }
            else
            {
                entry.Event = ToJsonByteString(events[counter]!);
                entry.ContentType = events[counter] is CloudEvent
                    ? MessagingConstants.ContentTypeCloudEvent
                    : (options?.ContentType ?? MessagingConstants.ContentTypeApplicationJson);
            }

            if (requestMetadata is not null)
            {
                entry.Metadata.Add(requestMetadata);
            }

            envelope.Entries.Add(entry);
            entryMap.Add(counter.ToString(), new BulkPublishEntry<TValue>(
                entry.EntryId, events[counter], entry.ContentType, entry.Metadata));
        }

        if (requestMetadata is not null)
        {
            foreach (var kvp in requestMetadata)
            {
                envelope.Metadata.Add(kvp.Key, kvp.Value);
            }
        }

        var grpcOptions = CreateCallOptions(cancellationToken);

        try
        {
            var response = await versionAwareClient.BulkPublishEventAsync(envelope, grpcOptions).ConfigureAwait(false);

            var failedEntries = new List<BulkPublishResponseFailedEntry<TValue>>();
            foreach (var entry in response.FailedEntries)
            {
                failedEntries.Add(new BulkPublishResponseFailedEntry<TValue>(entryMap[entry.EntryId], entry.Error));
            }

            return new BulkPublishResponse<TValue>(failedEntries);
        }
        catch (RpcException ex)
        {
            throw new DaprException(
                "Bulk Publish operation failed: the Dapr endpoint indicated a failure. See InnerException for details.",
                ex);
        }
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    private async Task MakePublishRequest(
        string pubsubName,
        string topicName,
        ByteString? content,
        Dictionary<string, string>? metadata,
        string? contentType,
        CancellationToken cancellationToken)
    {
        var envelope = new P.PublishEventRequest { PubsubName = pubsubName, Topic = topicName };

        if (content is not null)
        {
            envelope.Data = content;
            envelope.DataContentType = contentType ?? MessagingConstants.ContentTypeApplicationJson;
        }

        if (metadata is not null)
        {
            foreach (var kvp in metadata)
            {
                envelope.Metadata.Add(kvp.Key, kvp.Value);
            }
        }

        var grpcOptions = CreateCallOptions(cancellationToken);

        try
        {
            await Client.PublishEventAsync(envelope, grpcOptions).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new DaprException(
                "Publish operation failed: the Dapr endpoint indicated a failure. See InnerException for details.",
                ex);
        }
    }

    private CallOptions CreateCallOptions(CancellationToken cancellationToken)
    {
        var headers = DaprApiToken is null
            ? null
            : new Metadata { { "dapr-api-token", DaprApiToken } };
        return new CallOptions(headers: headers, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Builds the publish metadata bag from <see cref="PublishOptions"/>, projecting the
    /// CloudEvent override fields into <c>cloudevent.*</c> metadata keys as the Dapr runtime expects.
    /// </summary>
    private static Dictionary<string, string> BuildPublishMetadata(PublishOptions options, object? data)
    {
        var metadata = new Dictionary<string, string>(options.Metadata);

        if (options.Id is not null) metadata["cloudevent.id"] = options.Id;
        if (options.Source is not null) metadata["cloudevent.source"] = options.Source.ToString();
        if (options.Type is not null) metadata["cloudevent.type"] = options.Type;
        if (options.Subject is not null) metadata["cloudevent.subject"] = options.Subject;
        if (options.TraceParent is not null) metadata["cloudevent.traceparent"] = options.TraceParent;
        if (options.TraceState is not null) metadata["cloudevent.tracestate"] = options.TraceState;

        return metadata;
    }

    private ByteString ToJsonByteString<T>(T data) =>
        ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(data, JsonSerializerOptions));

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.HttpClient.Dispose();
        }
    }
}
