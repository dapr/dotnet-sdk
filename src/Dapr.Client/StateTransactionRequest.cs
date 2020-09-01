// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System.Threading;
    using Dapr.Client;

    /// <summary>
    /// Represents a single request in in a StateTransaction.
    /// </summary>
    /// <typeparam name="TValue">The data type of the value.</typeparam>
    public sealed class StateTransactionRequest<TValue>
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="StateEntry{TValue}"/> class.
        /// </summary>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value.</param>
        /// <param name="operationType">The operation type.</param>
        public StateTransactionRequest(string key, TValue value, string operationType)
        {
            ArgumentVerifier.ThrowIfNull(key, nameof(key));

            this.Key = key;
            this.Value = value;
            this.OperationType = operationType;
        }


        /// <summary>
        /// Gets the state key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets or sets the value locally.
        /// </summary>
        public TValue Value { get; set; }

        /// <summary>
        /// The Operation type.
        /// </summary>
        public string OperationType { get; }
    }
}
