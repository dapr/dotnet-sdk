// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    /// <summary>
    /// Concurrency mode for state operations with Dapr.
    /// </summary>
    public enum ConcurrencyMode
    {
        /// <summary>
        /// State operations will be handled in a first-write-wins fashion
        /// </summary>
        FirstWrite,

        /// <summary>
        /// State operations will be handled in a last-write-wins fashion
        /// </summary>
        LastWrite,
    }
}
