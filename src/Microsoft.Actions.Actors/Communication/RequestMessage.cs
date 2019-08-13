// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    internal class RequestMessage : IRequestMessage
    {
        private readonly IRequestMessageHeader header;
        private readonly IRequestMessageBody msgBody;

        public RequestMessage(IRequestMessageHeader header, IRequestMessageBody msgBody)
        {
            this.header = header;
            this.msgBody = msgBody;
        }

        public IRequestMessageHeader GetHeader()
        {
            return this.header;
        }

        public IRequestMessageBody GetBody()
        {
            return this.msgBody;
        }
    }
}
