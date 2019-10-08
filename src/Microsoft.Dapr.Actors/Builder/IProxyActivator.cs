// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Builder
{
    using Microsoft.Dapr.Actors.Client;

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
