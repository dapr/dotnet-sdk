// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client;

    /// <summary>
    /// Represents a value in the Dapr state store.
    /// </summary>
    /// <typeparam name="TValue">The data type of the value.</typeparam>
    public sealed class StateEntry<TValue>
    {
        private readonly DaprClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateEntry{TValue}"/> class.
        /// </summary>
        /// <param name="client">The <see cref="DaprClient" /> instance used to retrieve the value.</param>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value.</param>
        /// <param name="etag">The ETag.</param>
        /// <remarks>
        /// Application code should not need to create instances of <see cref="StateEntry{T}" />. Use  
        /// <see cref="DaprClient.GetStateEntryAsync{TValue}(string, string, ConsistencyMode?, IReadOnlyDictionary{string, string}, CancellationToken)" /> to access
        /// state entries.
        /// </remarks>
        public StateEntry(DaprClient client, string storeName, string key, TValue value, string etag)
        {
            ArgumentVerifier.ThrowIfNull(client, nameof(client));
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

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
        /// Gets or sets the value locally.  This is not sent to the state store until an API (e.g. <see cref="DeleteAsync(StateOptions, IReadOnlyDictionary{string, string}, CancellationToken)"/> is called.
        /// </summary>
        public TValue Value { get; set; }

        /// <summary>
        /// The ETag.
        /// </summary>
        public string ETag { get; }

        /// <summary>
        /// Deletes the entry associated with <see cref="Key" /> in the state store.
        /// </summary>
        /// <param name="stateOptions">A <see cref="StateOptions"/> object.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This depends on the state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public Task DeleteAsync(StateOptions stateOptions = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default)
        {
            // ETag is intentionally not specified
            return this.client.DeleteStateAsync(this.StoreName, this.Key, stateOptions, metadata, cancellationToken);
        }

        /// <summary>
        /// Saves the current value of <see cref="Value" /> to the state store.
        /// </summary>        
        /// <param name="stateOptions">Options for Save state operation.</param>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This is dependent on the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public Task SaveAsync(StateOptions stateOptions = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default)
        {
            // ETag is intentionally not specified
            return this.client.SaveStateAsync(
                storeName: this.StoreName,
                key: this.Key,
                value: this.Value,
                metadata: metadata,
                stateOptions: stateOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Tries to save the state using the etag to the Dapr state. State store implementation will allow the update only if the attached ETag matches with the latest ETag in the state store.
        /// </summary>
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This is dependent on the type of state store used.</param>
        /// <param name="stateOptions">Options for Save state operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.  If the wrapped value is true the operation suceeded.</returns>
        public Task<bool> TrySaveAsync(StateOptions stateOptions = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default)
        {
            return this.client.TrySaveStateAsync(
                this.StoreName,
                this.Key,
                this.Value,
                this.ETag,
                stateOptions,
                metadata,
                cancellationToken);
        }

        /// <summary>
        /// Tries to delete the the state using the 
        /// <see cref="ETag"/> from the Dapr state. State store implementation will allow the delete only if the attached ETag matches with the latest ETag in the state store.
        /// </summary>
        /// <param name="stateOptions">Options for Save state operation.</param>        
        /// <param name="metadata">An key/value pair that may be consumed by the state store.  This depends on the state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.  If the wrapped value is true the operation suceeded.</returns>
        public Task<bool> TryDeleteAsync(StateOptions stateOptions = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default)
        {
            return this.client.TryDeleteStateAsync(
                this.StoreName,
                this.Key,
                this.ETag,
                stateOptions,
                metadata,
                cancellationToken);
        }
    }
}
