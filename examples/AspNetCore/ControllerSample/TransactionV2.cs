// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace ControllerSample
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Represents a transaction used by sample code.
    /// </summary>
    public class TransactionV2
    {
        /// <summary>
        /// Gets or sets account id for the transaction.
        /// </summary>
        [Required]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets amount for the transaction.
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        /// <summary>
        /// Gets or sets channel from which this transaction was received.
        /// </summary>
        [Required]
        public string Channel { get; set; }
    }
}