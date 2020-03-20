// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Operation retry options when perfroming operations with Dapr.
    /// </summary>
    public class RetryOptions
    {
        /// <summary>
        /// Initial delay between retries, in milliseconds.
        /// </summary>
        /// <remarks>
        ///  The interval remains constant for <see cref="RetryMode.Linear"/>.
        ///  The interval is doubled after each retry for <see cref="RetryMode.Exponential"/>. So, for exponential pattern, the delay after attempt n will be interval*2^(n-1).
        /// </remarks>
        public TimeSpan? RetryInterval { get; set; }

        /// <summary>
        /// Retry pattern, can be either linear or exponential.
        /// </summary>
        public RetryMode? RetryMode { get; set; }

        /// <summary>
        /// Maximum number of retries.
        /// </summary>
        public int? RetryThreshold { get; set; }
    }
}
