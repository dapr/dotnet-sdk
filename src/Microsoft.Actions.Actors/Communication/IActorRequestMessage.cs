// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    /// <summary>
    /// Defines the interface that must be implemented for create Remoting Request Message.
    /// </summary>
    public interface IActorRequestMessage
    {
        /// <summary>
        /// Gets the Remoting Request Message Header.
        /// </summary>
        /// <returns>IServiceRemotingRequestMessageHeader.</returns>
        IActorRequestMessageHeader GetHeader();

        /// <summary>
        /// Gets the Remoting Request Message Body.</summary>
        /// <returns>IServiceRemotingRequestMessageBody.</returns>
        IActorMessageBody GetBody();
    }
}
