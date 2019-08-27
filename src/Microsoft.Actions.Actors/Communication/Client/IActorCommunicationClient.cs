// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the interface for the client that communicate with an actor.
    /// </summary>
    internal interface IActorCommunicationClient
    {
        /// <summary>
        /// Gets the id of the actor this client communicates with.
        /// </summary>
        ActorId ActorId { get; }
    }
}
