// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    /// <summary>
    /// A factory for creating <see cref="IActorCommunicationClient">actions communication clients.</see>.
    /// </summary>
    internal interface IActorCommunicationClientFactory
    {
        /// <summary>
        /// Gets a factory for creating the remoting message bodies.
        /// </summary>
        /// <returns>A factory for creating the remoting message bodies.</returns>
        IActorMessageBodyFactory GetRemotingMessageBodyFactory();

        /// <summary>
        /// Gets actor communication client.
        /// </summary>
        /// <param name="actorId"> Actor Id.</param>
        /// <param name="actorType"> Actor Type.</param>
        /// <returns>A factory for creating the remoting message bodies.</returns>
        ActorCommunicationClient GetClient(ActorId actorId, string actorType);
    }
}
