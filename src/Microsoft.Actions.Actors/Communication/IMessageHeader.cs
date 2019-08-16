// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System.IO;

    /// <summary>
    /// Defines an interface that must be implemented to provide message header for the serialized Message.
    /// </summary>
    internal interface IMessageHeader
    {
        /// <summary>
        /// Returns the byte array to be sent over the wire.
        /// </summary>
        /// <returns>Byte Array.</returns>
        byte[] GetSendBytes();

        /// <summary>
        /// Gets the Recieved Stream .
        /// </summary>
        /// <returns>Stream .</returns>
        Stream GetReceivedBuffer();
    }
}
