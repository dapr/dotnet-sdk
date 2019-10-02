// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Communication
{
    /// <summary>
    /// Defines the interface that must be implemented for create Actor Request Message.
    /// </summary>
    public interface IActorRequestMessage
    {
        /// <summary>
        /// Gets the Actor Request Message Header.
        /// </summary>
        /// <returns>IActorRequestMessageHeader.</returns>
        IActorRequestMessageHeader GetHeader();

        /// <summary>
        /// Gets the Actor Request Message Body.</summary>
        /// <returns>IActorRequestMessageBody.</returns>
        IActorRequestMessageBody GetBody();
    }
}
