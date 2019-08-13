// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;

    /// <summary>
    /// Defines an interface that must be implemented to provide message body for the serialized Message.
    /// </summary>
    public interface IOutgoingMessageBody
    {
        /// <summary>
        /// Gets the Send Buffers.
        /// </summary>
        /// <returns>Array of bytes.</returns>
        byte[] GetSendBytes();
    }
}
