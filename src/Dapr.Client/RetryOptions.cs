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
        /// 
        /// </summary>
        public TimeSpan? RetryInterval { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public RetryMode? RetryMode { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int? RetryThreshold { get; set; }
    }
}
