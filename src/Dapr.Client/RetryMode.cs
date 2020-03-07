// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    /// <summary>
    /// Represents the delay between retries.  See https://github.com/dapr/docs/blob/master/reference/api/state.md#retry-policy
    /// </summary>
    public enum RetryMode
    {
        /// <summary>
        /// The delay between retries is constant.
        /// </summary>
        Linear,

        /// <summary>
        /// The delay between retries doubles each time.
        /// </summary>
        Exponential,
    }
}
