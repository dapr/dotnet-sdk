// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    /// <summary>
    /// Represents the outgoing message body to be sent over the wire.
    /// </summary>
    public sealed class OutgoingMessageBody : IOutgoingMessageBody
    {
        private readonly byte[] bodyBuffers;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutgoingMessageBody"/> class.
        /// Creates OutgoingMessageBody with byte array.
        /// </summary>
        /// <param name="outgoingBodyBuffers">Byte Array .</param>
        public OutgoingMessageBody(byte[] outgoingBodyBuffers)
        {
            this.bodyBuffers = outgoingBodyBuffers;
        }

        /// <summary>
        /// Returns the Buffers to be sent over the wire.
        /// </summary>
        /// <returns>array of bytes .</returns>
        public byte[] GetSendBytes()
        {
            return this.bodyBuffers;
        }
    }
}
