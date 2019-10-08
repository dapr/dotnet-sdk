// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace RoutingSample
{
    /// <summary>
    /// Represents a transaction used by sample code.
    /// </summary>
    public class Transaction
    {
        /// <summary>
        /// Gets or sets account id for the transaction.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets amount for the transaction.
        /// </summary>
        public decimal Amount { get; set; }
    }
}