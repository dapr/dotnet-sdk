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
/// Publish-side client for the Dapr pub/sub building block. Implemented by the runtime
/// gRPC client bundled in the <c>Dapr.Messaging</c> meta-package.
/// </summary>
public interface IDaprPublishSubscribeClient
{
    /// <summary>
    /// Publishes an event to the specified topic. The data is JSON-serialized and sent
    /// with <c>application/json</c> (or <c>application/cloudevents+json</c> when <typeparamref name="TData"/>
    /// is a <see cref="CloudEvent"/>).
    /// </summary>
    Task PublishEventAsync<TData>(
        string pubsubName,
        string topicName,
        TData data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event to the specified topic with typed publish options.
    /// </summary>
    Task PublishEventAsync<TData>(
        string pubsubName,
        string topicName,
        TData data,
        PublishOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an empty event to the specified topic.
    /// </summary>
    Task PublishEventAsync(
        string pubsubName,
        string topicName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a raw byte payload to the specified topic.
    /// </summary>
    Task PublishByteEventAsync(
        string pubsubName,
        string topicName,
        ReadOnlyMemory<byte> data,
        string dataContentType = MessagingConstants.ContentTypeApplicationJson,
        PublishOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk publishes multiple events to the specified topic.
    /// </summary>
    Task<BulkPublishResponse<TValue>> BulkPublishEventAsync<TValue>(
        string pubsubName,
        string topicName,
        IReadOnlyList<TValue> events,
        PublishOptions? options = null,
        CancellationToken cancellationToken = default);
}
