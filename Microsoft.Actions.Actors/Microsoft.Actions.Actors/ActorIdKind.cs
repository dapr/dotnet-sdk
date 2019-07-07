// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    /// <summary>
    /// Specifies the type of the ID value for an <see cref="ActorId"/>.
    /// </summary>
    public enum ActorIdKind
    {
        /// <summary>
        /// Represents ID value of type <see cref="long"/>.
        /// </summary>
        Long = 0,

        /// <summary>
        /// Represents ID value of type <see cref="System.Guid"/>.
        /// </summary>
        Guid = 1,

        /// <summary>
        /// Represents ID value of type <see cref="string"/>.
        /// </summary>
        String = 2,
    }
}
