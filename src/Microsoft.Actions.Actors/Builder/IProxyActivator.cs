// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Builder
{
    using Microsoft.Actions.Actors.Client;

    /// <summary>
    /// Interface to create <see cref="ActorProxy"/> objects.
    /// </summary>
    public interface IProxyActivator
    {
        /// <summary>
        /// Create the instance of the generated proxy type.
        /// </summary>
        /// <returns>An instance of the generated proxy as <see cref="IActorProxy"/>type.</returns>
        IActorProxy CreateInstance();
    }
}
