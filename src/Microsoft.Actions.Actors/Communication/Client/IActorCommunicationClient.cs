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
    public interface IActorCommunicationClient
    {
        /// <summary>
        /// Gets the id of the actor this client communicates with.
        /// </summary>
        ActorId ActorId { get; }

        /// <summary>
        /// Send a remoting request to the service and gets a response back.
        /// </summary>
        /// <param name="requestRequestMessage">The request message.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation for remote method call.
        /// The result of the task contains the response for the request.</returns>
        Task<IResponseMessage> RequestResponseAsync(IRequestMessage requestRequestMessage);

        /// <summary>
        /// Sends a one-way message to the service.
        /// </summary>
        /// <param name="requestMessage">The one-way message.</param>
        void SendOneWay(IRequestMessage requestMessage);
    }
}
