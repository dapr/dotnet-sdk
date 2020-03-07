// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    /// <summary>
    /// See https://github.com/dapr/docs/blob/master/reference/api/state.md#consistency
    /// </summary>
    public enum ConsistencyMode
    {
        /// <summary>
        /// Eventual consistency.
        /// </summary>
        Eventual,

        /// <summary>
        /// Strong consistency.
        /// </summary>
        Strong,
    }
}
