// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System.IO;

    /// <summary>
    /// Serialized Message Body received from an incoming connection.
    /// </summary>
    public sealed class IncomingMessageBody : IIncomingMessageBody
    {
        private readonly Stream receivedBufferStream;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="IncomingMessageBody"/> class.
        /// Creates an incoming Message Body with the received stream .
        /// </summary>
        /// <param name="receivedBufferStream">Recieved Stream .</param>
        public IncomingMessageBody(Stream receivedBufferStream)
        {
            this.receivedBufferStream = receivedBufferStream;
        }

        /// <summary>
        /// Return the Received Buffer Stream.
        /// </summary>
        /// <returns>Stream.</returns>
        public Stream GetReceivedBuffer()
        {
            return this.receivedBufferStream;
        }

        /// <summary>
        /// Dispose the Received Buffer stream.
        /// </summary>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.receivedBufferStream.Dispose();
            }
        }
    }
}
