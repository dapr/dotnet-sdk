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
    /// Options when perfroming state operations with Dapr.
    /// </summary>
    public class StateOptions
    {
        /// <summary>
        /// 
        /// </summary>
        public ConsistencyMode? Consistency { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ConcurrencyMode? Concurrency { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public RetryOptions RetryOptions {get; set;}
    }
}
