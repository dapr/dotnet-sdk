// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a value in the Dapr state store.
    /// </summary>
    /// <typeparam name="TValue">The data type.</typeparam>
    public sealed class StateEntry<TValue>
    {
        private readonly StateClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateEntry{TValue}"/> class.
        /// </summary>
        /// <param name="client">The <see cref="StateClient" /> instance used to retrieve the value.</param>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// Application code should not need to create instances of <see cref="StateEntry{T}" />. Use
        /// <see cref="StateClient.GetStateEntryAsync{TValue}(string, string, CancellationToken)" /> to access
        /// state entries.
        /// </remarks>
        public StateEntry(StateClient client, string storeName, string key, TValue value)
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
        /// Deletes the entry associated with <see cref="Key" /> in the state store.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public ValueTask DeleteAsync(CancellationToken cancellationToken = default)
        {
            return this.client.DeleteStateAsync(this.StoreName, this.Key, cancellationToken);
        }

        /// <summary>
        /// Saves the current value of <see cref="Value" /> to the state store.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public ValueTask SaveAsync(CancellationToken cancellationToken = default)
        {
            return this.client.SaveStateAsync(this.StoreName, this.Key, this.Value, cancellationToken);
        }
    }
}
