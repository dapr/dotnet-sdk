// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr
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
        /// Initializes a new <see cref="StateEntry{T}" /> instance.
        /// </summary>
        /// <param name="client">The <see cref="StateClient" /> instance used to retrieve the value.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// Application code should not need to create instances of <see cref="StateEntry{T}" />. Use 
        /// <see cref="StateClient.GetStateEntryAsync{TValue}(string, CancellationToken)" /> to access
        /// state entries.
        /// </remarks>
        public StateEntry(StateClient client, string key, TValue value)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            this.Key = key;
            this.Value = value;
            this.client = client;
        }

        /// <summary>
        /// Gets the state key.
        /// </summary>
        public string Key { get; }
        
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public TValue Value { get; set; }

        /// <summary>
        /// Saves the the current value of <see cref="Value" /> to the state store.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public ValueTask SaveAsync(CancellationToken cancellationToken = default)
        {
            return this.client.SaveStateAsync(this.Key, this.Value, cancellationToken);
        }
    }
}
