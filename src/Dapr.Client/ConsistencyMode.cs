// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    /// <summary>
    /// Consistency mode for state operations with Dapr.
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
