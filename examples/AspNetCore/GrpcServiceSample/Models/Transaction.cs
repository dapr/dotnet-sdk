﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace GrpcServiceSample.Models
{
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
