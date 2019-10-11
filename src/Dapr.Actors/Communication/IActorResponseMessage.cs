// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    /// <summary>
    /// Defines an interface that must be implemented to provide  a actor response message for remoting Api.
    /// </summary>
    public interface IActorResponseMessage
    {
        /// <summary>
        /// Gets the header of the response message.
        /// </summary>
        /// <returns>The header of this response message.</returns>
        IActorResponseMessageHeader GetHeader();

        /// <summary>
        /// Gets the body of the response message.
        /// </summary>
        /// <returns>The body of this response message.</returns>
        IActorResponseMessageBody GetBody();
    }
}
