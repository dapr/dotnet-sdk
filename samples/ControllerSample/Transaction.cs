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
    public class Transaction
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
    }
}