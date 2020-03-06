// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc;

    /// <summary>
    /// Defines methods for clients interacting with the Dapr endpoints.
    /// </summary>
    public abstract class DaprClient
    {
        /// <summary>
        /// Publish a new event to the specified topic.
        /// </summary>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="publishContent">The contents of the event.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
        /// <returns>A <see cref="Task" />.</returns>
        public abstract Task PublishEventAsync<TRequest>(string topicName, TRequest publishContent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish a new event to the specified topic.
        /// </summary>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" />.</returns>
        public abstract Task PublishEventAsync(string topicName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="name"></param>
        /// <param name="content"></param>
        /// <param name="metadata"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public abstract Task InvokeBindingAsync<TRequest>(
           string name,
           TRequest content,
           IReadOnlyDictionary<string, string> metadata = default,
           CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on another dapr app.
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send.</typeparam>
        /// <typeparam name="TResponse">The type of the object in the response.</typeparam>
        /// <param name="serviceName">The dapr app to invoke a method on.</param>
        /// <param name="methodName">The method to invoke.</param>
        /// <param name="data">Data to pass to the method</param>
        /// <param name="metadata">Metadata</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        public abstract Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
            string serviceName,
            string methodName,
            TRequest data,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode: Strong or Eventual.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will return the value when the operation has completed.</returns>
        public abstract ValueTask<TValue> GetStateAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store and an ETag.
        /// </summary>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode: Strong or Eventual.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns></returns>
        public abstract ValueTask<StateAndETag<TValue>> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a <see cref="StateEntry{T}" /> for the current value associated with the <paramref name="key" /> from
        /// the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode: Strong or Eventual.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will return the <see cref="StateEntry{T}" /> when the operation has completed.</returns>
        public async ValueTask<StateEntry<TValue>> GetStateEntryAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(storeName))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(storeName));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            var stateAndETag = await this.GetStateAndETagAsync<TValue>(storeName, key, consistencyMode, cancellationToken);
            return new StateEntry<TValue>(this, storeName, key, stateAndETag.Data, stateAndETag.ETag);
        }

        /// <summary>
        /// Saves the provided <paramref name="value" /> associated with the provided <paramref name="key" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value to save.</param>
        /// <param name="etag">An ETag.</param>
        /// <param name="metadata">An ETag.</param>
        /// <param name="stateRequestOptions">A <see cref="StateRequestOptions" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public abstract ValueTask SaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            IReadOnlyDictionary<string, string> metadata,
            StateRequestOptions stateRequestOptions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the value associated with the provided <paramref name="key" /> in the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="etag">An ETag.</param>
        /// <param name="stateOptions">A <see cref="StateOptions" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public abstract ValueTask DeleteStateAsync(string storeName, string key, string etag, StateOptions stateOptions = default, CancellationToken cancellationToken = default);
    }
}
