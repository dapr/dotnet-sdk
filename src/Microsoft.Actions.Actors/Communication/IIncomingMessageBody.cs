// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;
    using System.IO;

    /// <summary>
    /// Defines an interface that must be implemented to provide message body for the serialized Message.
    /// </summary>
    public interface IIncomingMessageBody : IDisposable
    {
        /// <summary>
        /// Get the Received Stream.
        /// </summary>
        /// <returns>Represents Recieved Buffer Stream.</returns>
        Stream GetReceivedBuffer();
    }
}
