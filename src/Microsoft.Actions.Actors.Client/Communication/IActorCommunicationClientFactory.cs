// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client.Communication
{
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Communication;    

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
        /// Get a communication client.
        /// </summary>
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task">Task</see> that represents outstanding operation. The result of the Task is
        /// the CommunicationClient(<see cref="IActorCommunicationClient" />) object.
        /// </returns>
        Task<IActionsInteractor> GetClientAsync();
    }
}
