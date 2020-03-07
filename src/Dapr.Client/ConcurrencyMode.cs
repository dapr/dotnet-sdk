// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    /// <summary>
    /// See https://github.com/dapr/docs/blob/master/reference/api/state.md#concurrency
    /// </summary>
    public enum ConcurrencyMode
    {
        /// <summary>
        /// 
        /// </summary>
        FirstWrite,

        /// <summary>
        /// 
        /// </summary>
        LastWrite,
    }
}
