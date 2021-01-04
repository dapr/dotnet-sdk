// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines client methods for interacting with Dapr endpoints.
    /// Use <see cref="DaprClientBuilder"/> to create <see cref="DaprClient"/>
    /// </summary>
    public abstract class DaprClient
    {
        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="data">The  event data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TData">The data type of the object that will be serialized.</typeparam>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task PublishEventAsync(string pubsubName, string topicName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes an output binding.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="bindingName">The name of the binding to sent the event to.</param>
        /// <param name="operation">The type of operation to perform on the binding.</param>
        /// <param name="data">The data of the event to send.</param>
        /// <param name="metadata">An open key/value pair that may be consumed by the binding component.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task InvokeBindingAsync<TRequest>(
            string bindingName,
            string operation,
            TRequest data,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes an output binding.
        /// </summary>
        /// <typeparam name="TRequest">The type of the object for the data to send.</typeparam>
        /// <typeparam name="TResponse">The type of the object for the return value.</typeparam>
        /// <param name="bindingName">The name of the binding to sent the event to.</param>
        /// <param name="operation">The type of operation to perform on the binding.</param>
        /// <param name="data">The data of the event to send.</param>
        /// <param name="metadata">An open key/value pair that may be consumed by the binding component.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{T}" /> that will complete when the operation has completed.</returns>
        public abstract ValueTask<TResponse> InvokeBindingAsync<TRequest, TResponse>(
            string bindingName,
            string operation,
            TRequest data,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>        
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>        
        /// <param name="httpOptions">Additional fields that may be needed if the receiving app is listening on HTTP.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>      
        public abstract Task InvokeMethodAsync(
            string appId,
            string methodName,
            HttpInvocationOptions httpOptions = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send.</typeparam>       
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>  
        /// <param name="data">Data to pass to the method</param>
        /// <param name="httpOptions">Additional fields that may be needed if the receiving app is listening on HTTP.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.</returns>        
        public abstract Task InvokeMethodAsync<TRequest>(
            string appId,
            string methodName,
            TRequest data,
            HttpInvocationOptions httpOptions = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>        
        /// <typeparam name="TResponse">The type of the object in the response.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>         
        /// <param name="httpOptions">Additional fields that may be needed if the receiving app is listening on HTTP.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.</returns>    
        public abstract ValueTask<TResponse> InvokeMethodAsync<TResponse>(
            string appId,
            string methodName,
            HttpInvocationOptions httpOptions = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send.</typeparam>
        /// <typeparam name="TResponse">The type of the object in the response.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>  
        /// <param name="data">Data to pass to the method</param>      
        /// <param name="httpOptions">Additional fields that may be needed if the receiving app is listening on HTTP.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.</returns>  
        public abstract ValueTask<TResponse> InvokeMethodAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            HttpInvocationOptions httpOptions = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>
       /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>  
        /// <param name="data">Data to pass to the method</param>      
        /// <param name="httpOptions">Additional fields that may be needed if the receiving app is listening on HTTP.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{InvokeResponse}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<InvocationResponse<TResponse>> InvokeMethodWithResponseAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            HttpInvocationOptions httpOptions = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on a Dapr app.
        /// </summary>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>  
        /// <param name="data">Byte array to pass to the method</param>      
        /// <param name="httpOptions">Additional fields that may be needed if the receiving app is listening on HTTP.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.</returns>  
        public abstract Task<InvocationResponse<byte[]>> InvokeMethodRawAsync(
            string appId,
            string methodName,
            byte[] data,
            HttpInvocationOptions httpOptions = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of state store to read from.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This is dependent on the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type of the value to read.</typeparam>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.</returns>
        public abstract ValueTask<TValue> GetStateAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, Dictionary<string, string> metadata = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a list of values associated with the <paramref name="keys" /> from the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of state store to read from.</param>
        /// <param name="keys">The list of keys to get values for.</param>
        /// <param name="parallelism">The number of concurrent get operations the Dapr runtime will issue to the state store. a value equal to or smaller than 0 means max parallelism.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{IReadOnlyList}" /> that will return the list of values when the operation has completed.</returns>

        public abstract ValueTask<IReadOnlyList<BulkStateItem>> GetBulkStateAsync(string storeName, IReadOnlyList<string> keys, int? parallelism, CancellationToken cancellationToken = default);
        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store and an ETag.
        /// </summary>
        /// <typeparam name="TValue">The data type of the value to read.</typeparam>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.  This wraps the read value and an ETag.</returns>
        public abstract ValueTask<(TValue value, string etag)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default);

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
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

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
        /// <typeparam name="TValue">The data type of the value to save.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public abstract Task SaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tries to save the state <paramref name="value" /> associated with the provided <paramref name="key" /> using the 
        /// <paramref name="etag"/> to the Dapr state. State store implementation will allow the update only if the attached ETag matches with the latest ETag in the state store.
        /// store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value to save.</param>
        /// <param name="etag">An ETag.</param>        
        /// <param name="stateOptions">Options for performing save state operation.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This depends on the state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type of the value to save.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.  If the wrapped value is true the operation succeeded.</returns>
        public abstract ValueTask<bool> TrySaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the provided <paramref name="operations" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="operations">A list of StateTransactionRequests.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This depends on the state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public abstract Task ExecuteStateTransactionAsync(
            string storeName,
            IReadOnlyList<StateTransactionRequest> operations,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the value associated with the provided <paramref name="key" /> in the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="stateOptions">A <see cref="StateOptions" />.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This depends on the state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task DeleteStateAsync(
            string storeName,
            string key,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tries to delete the the state associated with the provided <paramref name="key" /> using the 
        /// <paramref name="etag"/> from the Dapr state. State store implementation will allow the delete only if the attached ETag matches with the latest ETag in the state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="etag">An ETag.</param>
        /// <param name="stateOptions">A <see cref="StateOptions" />.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This depends on the state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.  If the wrapped value is true the operation suceeded.</returns>
        public abstract ValueTask<bool> TryDeleteStateAsync(
            string storeName,
            string key,
            string etag,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the secret value ffrom the secret store.
        /// </summary>
        /// <param name="storeName">Secret store name.</param>
        /// <param name="key">Key for the secret.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the secret store.  This depends on the secret store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{T}" /> that will return the value when the operation has completed.</returns>
        public abstract ValueTask<Dictionary<string, string>> GetSecretAsync(
            string storeName,
            string key,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);
    }
}
