// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Runtime
{
    /// <summary>
    /// Represents the kind of state change for an actor state when saves change is called to a set of actor states.
    /// </summary>
    public enum StateChangeKind
    {
        /// <summary>
        /// No change in state
        /// </summary>
        None = 0,

        /// <summary>
        /// The state needs to be added.
        /// </summary>
        Add = 1,

        /// <summary>
        /// The state needs to be updated.
        /// </summary>
        Update = 2,

        /// <summary>
        /// The state needs to be removed.
        /// </summary>
        Remove = 3,
    }
}
