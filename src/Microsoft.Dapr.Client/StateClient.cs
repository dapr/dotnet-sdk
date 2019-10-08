// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A client for interacting with the Dapr state store.
    /// </summary>
    public abstract class StateClient
    {
        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store.
        /// </summary>
        /// <param name="key">The state key.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will return the value when the operation has completed.</returns>
        public abstract ValueTask<TValue> GetStateAsync<TValue>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a <see cref="StateEntry{T}" /> for the current value associated with the <paramref name="key" /> from
        /// the Dapr state store.
        /// </summary>
        /// <param name="key">The state key.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will return the <see cref="StateEntry{T}" /> when the operation has completed.</returns>
        public async ValueTask<StateEntry<TValue>> GetStateEntryAsync<TValue>(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            var value = await this.GetStateAsync<TValue>(key, cancellationToken);
            return new StateEntry<TValue>(this, key, value);
        }

        /// <summary>
        /// Saves the provided <paramref name="value" /> associated with the provided <paramref name="key" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value to save.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public abstract ValueTask SaveStateAsync<TValue>(string key, TValue value, CancellationToken cancellationToken = default);
    }
}
