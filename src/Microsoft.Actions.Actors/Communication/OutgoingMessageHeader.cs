// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;
    using System.IO;

    internal sealed class OutgoingMessageHeader : IMessageHeader
    {
        private readonly byte[] outgoingBuffer;

        public OutgoingMessageHeader()
        {
            this.outgoingBuffer = null;
        }

        public OutgoingMessageHeader(byte[] buffer)
        {
            this.outgoingBuffer = buffer;
        }

        public byte[] GetSendBytes()
        {
            return this.outgoingBuffer;
        }

        public Stream GetReceivedBuffer()
        {
            throw new NotImplementedException("This method is not valid on outgoing messages");
        }
    }
}
