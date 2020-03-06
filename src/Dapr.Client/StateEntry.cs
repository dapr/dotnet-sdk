// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client;
    using Dapr.Client.Autogen.Grpc;

    /// <summary>
    /// Represents a value in the Dapr state store.
    /// </summary>
    /// <typeparam name="TValue">The data type.</typeparam>
    public sealed class StateEntry<TValue>
    {
        private readonly DaprClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateEntry{TValue}"/> class.
        /// </summary>
        /// <param name="client">The <see cref="DaprClientGrpc" /> instance used to retrieve the value.</param>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value.</param>
        /// <param name="etag">The etag.</param>
        /// <remarks>
        /// Application code should not need to create instances of <see cref="StateEntry{T}" />. Use
        /// <see cref="Dapr.Client.DaprClient.GetStateEntryAsync{TValue}(string, string, ConsistencyMode?, CancellationToken)" /> to access
        /// state entries.
        /// </remarks>
        public StateEntry(DaprClient client, string storeName, string key, TValue value, string etag)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (string.IsNullOrEmpty(storeName))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(storeName));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            this.StoreName = storeName;
            this.Key = key;
            this.Value = value;
            this.client = client;

            this.ETag = etag;
        }

        /// <summary>
        /// Gets the State Store Name.
        /// </summary>
        public string StoreName { get; }

        /// <summary>
        /// Gets the state key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public TValue Value { get; set; }

        /// <summary>
        /// The ETag.
        /// </summary>
        public string ETag { get; }

        /// <summary>
        /// Deletes the entry associated with <see cref="Key" /> in the state store.
        /// </summary>        
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public ValueTask DeleteAsync(CancellationToken cancellationToken = default)
        {
            return this.DeleteAsync(cancellationToken);
        }

        /// <summary>
        /// Deletes the entry associated with <see cref="Key" /> in the state store.
        /// </summary>
        /// <param name="stateOptions">A <see cref="StateOptions"/> object.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public ValueTask DeleteAsync(StateOptions stateOptions = default, CancellationToken cancellationToken = default)
        {
            // ETag is intentionally not specified
            return this.client.DeleteStateAsync(this.StoreName, this.Key, null, stateOptions, cancellationToken);
        }

        /// <summary>
        /// Saves the current value of <see cref="Value" /> to the state store.
        /// </summary>        
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public ValueTask SaveAsync(CancellationToken cancellationToken = default)
        {
            return this.SaveAsync(null, null, cancellationToken);
        }

        /// <summary>
        /// Saves the current value of <see cref="Value" /> to the state store.
        /// </summary>
        /// <param name="metadata">Additional metadata.</param>
        /// <param name="stateRequestOptions">A <see cref="StateRequestOptions" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public ValueTask SaveAsync(IReadOnlyDictionary<string, string> metadata, StateRequestOptions stateRequestOptions, CancellationToken cancellationToken = default)
        {
            // ETag is intentionally not specified
            return this.client.SaveStateAsync(this.StoreName, this.Key, this.Value, null, metadata, stateRequestOptions, cancellationToken);
        }

        /// <summary>
        /// Saves the current value of <see cref="Value" /> to the state store.
        /// </summary>        
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public ValueTask<bool> TrySaveAsync(CancellationToken cancellationToken = default)
        {
            return this.TrySaveAsync(null, null, cancellationToken);
        }

        /// <summary>
        /// Saves the current value of <see cref="Value" /> to the state store.
        /// </summary>
        /// <param name="metadata">Additional metadata.</param>
        /// <param name="stateRequestOptions">A <see cref="StateRequestOptions" />.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public async ValueTask<bool> TrySaveAsync(IReadOnlyDictionary<string, string> metadata, StateRequestOptions stateRequestOptions, CancellationToken cancellationToken = default)
        {
            try
            {
                await this.client.SaveStateAsync(
                    this.StoreName, 
                    this.Key, 
                    this.Value, 
                    this.ETag, 
                    metadata, 
                    stateRequestOptions, 
                    cancellationToken);

                return true;
            }
            catch (Exception)
            {                
                // do not throw, return false
                // ? TODO: what type of exception is this?
            }

            return false;
        }
    }
}
