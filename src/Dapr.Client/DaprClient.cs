// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc;

    /// <summary>
    /// Defines client methods for interacting with Dapr endpoints.
    /// </summary>
    public abstract class DaprClient
    {
        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="publishContent">The contents of the event.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
        /// <returns>A <see cref="Task" />.</returns>
        public abstract Task PublishEventAsync<TRequest>(string topicName, TRequest publishContent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" />.</returns>
        public abstract Task PublishEventAsync(string topicName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes an output binding.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="name">The name of the binding to sent the event to.</param>
        /// <param name="content">The content of the event to send.</param>
        /// <param name="metadata">An open key/value pair that may be consumed by the binding component.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns></returns>
        public abstract Task InvokeBindingAsync<TRequest>(
            string name,
            TRequest content,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>        
        /// <param name="serviceName">The Dapr app to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>        
        /// <param name="metadata">A key/value pair to pass to the method to invoke.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns></returns>
        public abstract Task InvokeMethodAsync(
            string serviceName,
            string methodName,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send.</typeparam>       
        /// <param name="serviceName">The Dapr app to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>  
        /// <param name="data">Data to pass to the method</param>
        /// <param name="metadata">Metadata</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns></returns>
        public abstract Task InvokeMethodAsync<TRequest>(
            string serviceName,
            string methodName,
            TRequest data,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>        
        /// <typeparam name="TResponse">The type of the object in the response.</typeparam>
        /// <param name="serviceName">The Dapr app to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>         
        /// <param name="metadata">Metadata</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>Data returned from the invoked method on the target Dapr app.</returns>
        public abstract Task<TResponse> InvokeMethodAsync<TResponse>(
            string serviceName,
            string methodName,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send.</typeparam>
        /// <typeparam name="TResponse">The type of the object in the response.</typeparam>
        /// <param name="serviceName">The Dapr app to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>  
        /// <param name="data">Data to pass to the method</param>
        /// <param name="metadata">Metadata</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
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
        /// <param name="storeName">The name of state store to read from.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type of the value to read.</typeparam>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.</returns>
        public abstract ValueTask<TValue> GetStateAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store and an ETag.
        /// </summary>
        /// <typeparam name="TValue">The data type of the value to read.</typeparam>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.  This wraps the read value and an ETag.</returns>                                
        public abstract ValueTask<(TValue value, ETag eTag)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a <see cref="StateEntry{T}" /> for the current value associated with the <paramref name="key" /> from
        /// the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type of the value to read.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will return the <see cref="StateEntry{T}" /> when the operation has completed.</returns>
        public async ValueTask<StateEntry<TValue>> GetStateEntryAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default)
        {
            storeName.ThrowIfNullOrEmpty(nameof(storeName));
            key.ThrowIfNullOrEmpty(nameof(key));

            var (state, etag) = await this.GetStateAndETagAsync<TValue>(storeName, key, consistencyMode, cancellationToken);
            return new StateEntry<TValue>(this, storeName, key, state, etag);
        }

        /// <summary>
        /// Saves the provided <paramref name="value" /> associated with the provided <paramref name="key" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value to save.</param>        
        /// <param name="stateOptions">Options for performing save state operation.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This is dependent on the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public abstract ValueTask SaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,            
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the provided <paramref name="value" /> associated with the provided <paramref name="key" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value to save.</param>
        /// <param name="etag">An ETag.</param>        
        /// <param name="stateOptions">Options for performing save state operation.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This depends on the state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.  If the wrapped value is true the operation succeeded.</returns>
        public abstract ValueTask<bool> TrySaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            ETag etag,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the value associated with the provided <paramref name="key" /> in the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="stateOptions">A <see cref="StateOptions" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public abstract ValueTask DeleteStateAsync(
            string storeName,
            string key,
            StateOptions stateOptions = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the value associated with the provided <paramref name="key" /> in the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="etag">An ETag.</param>
        /// <param name="stateOptions">A <see cref="StateOptions" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.  If the wrapped value is true the operation suceeded.</returns>
        public abstract ValueTask<bool> TryDeleteStateAsync(
            string storeName,
            string key,
            ETag etag,
            StateOptions stateOptions = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the secret value ffrom the secret store.
        /// </summary>
        /// <param name="storeName">Secret store name.</param>
        /// <param name="key">Key for the secret.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the secret store.  This depends on the secret store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>The secret.</returns>
        public abstract Task<Dictionary<string, string>> GetSecretAsync(
            string storeName,
            string key,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);
    }
}
