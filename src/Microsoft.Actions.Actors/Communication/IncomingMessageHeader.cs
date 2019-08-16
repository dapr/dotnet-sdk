// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;
    using System.IO;

    internal sealed class IncomingMessageHeader : IMessageHeader
    {
        private readonly Stream receivedBufferStream;
        private bool isDisposed;

        public IncomingMessageHeader(Stream receivedBufferStream)
        {
            this.receivedBufferStream = receivedBufferStream;
        }

        public byte[] GetSendBytes()
        {
            throw new NotImplementedException("This method is not implemented for incoming messages");
        }

        public Stream GetReceivedBuffer()
        {
            return this.receivedBufferStream;
        }

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
