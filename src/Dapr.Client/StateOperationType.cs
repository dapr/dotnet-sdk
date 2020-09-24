// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    /// <summary>
    /// Operation type for state operations with Dapr.
    /// </summary>
    public enum StateOperationType
    {
        /// <summary>
        /// Upsert a new or existing state
        /// </summary>
        Upsert,

        /// <summary>
        /// Delete a state
        /// </summary>
        Delete,
    }
}
