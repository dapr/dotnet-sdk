// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    internal class ResponseMessage : IResponseMessage
    {
        private readonly IResponseMessageHeader header;
        private readonly IResponseMessageBody msgBody;

        public ResponseMessage(
            IResponseMessageHeader header,
            IResponseMessageBody msgBody)
        {
            this.header = header;
            this.msgBody = msgBody;
        }

        public IResponseMessageHeader GetHeader()
        {
            return this.header;
        }

        public IResponseMessageBody GetBody()
        {
            return this.msgBody;
        }
    }
}
