﻿// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Text.Json;using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.Clients.ProgrammaticClient;

/// <summary>
/// The base implementation of a programmatic pub/sub client.
/// </summary>
public abstract class DaprPubSubProgrammaticClient(Autogenerated.Dapr.DaprClient client, HttpClient httpClient, JsonSerializerOptions jsonSerializerOptions, string? daprApiToken = null) : IDaprPubSubProgrammaticClient
{
    private bool disposed;

    /// <summary>
    /// The HTTP client used by the client for calling the Dapr runtime.
    /// </summary>
    /// <remarks>
    /// Property exposed for testing purposes.
    /// </remarks>
    internal protected readonly HttpClient HttpClient = httpClient;

    /// <summary>
    /// The Dapr API token value.
    /// </summary>
    /// <remarks>
    /// Property exposed for testing purposes.
    /// </remarks>
    internal protected readonly string? DaprApiToken = daprApiToken;

    /// <summary>
    /// The autogenerated Dapr client.
    /// </summary>
    /// <remarks>
    /// Property exposed for testing purposes.
    /// </remarks>
    internal protected readonly Autogenerated.Dapr.DaprClient Client = client;

    /// <summary>
    /// The JSON serializer options.
    /// </summary>
    /// <remarks>
    /// Property exposed for testing purposes.
    /// </remarks>
    internal protected JsonSerializerOptions JsonSerializerOptions = jsonSerializerOptions;
    
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
    public abstract Task PublishEventAsync<TData>(
        string pubSubName,
        string topicName,
        TData? data = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) where TData : class;

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
    public abstract Task PublishEventAsync<TData>(
        string pubSubName,
        string topicName,
        IReadOnlyList<TData> data,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

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
    public abstract Task PublishEventAsync(
        string pubSubName,
        string topicName,
        ReadOnlyMemory<byte> data,
        string dataContentType = "application/json",
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// // Bulk Publishes multiple events to the specified topic.
    /// </summary>
    /// <param name="pubSubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic the request should be published to.</param>
    /// <param name="events">The list of events to be serialized and ublished.</param>
    /// <param name="metadata">A collection of optional metadata key/value pairs that will be provided to the component.
    /// The valid metadata keys and values are determined by the type of PubSub component used.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> that will complete when the operation has completed with a list of error
    /// messages accompanying any failed requests.</returns>
    public abstract Task<DaprBulkPublishResponse> BulkPublishEventAsync<TData>(
        string pubSubName,
        string topicName,
        IReadOnlyList<TData> events,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);
    
    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.disposed)
        {
            Dispose(disposing: true);
            this.disposed = true;
        }
    }

    /// <summary>
    /// Disposes the resources associated with the object.
    /// </summary>
    /// <param name="disposing"><c>true</c> if called by a call to the <c>Dispose</c> method; otherwise false.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
