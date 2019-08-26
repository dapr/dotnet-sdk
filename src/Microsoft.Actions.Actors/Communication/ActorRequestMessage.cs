// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    internal class ActorRequestMessage : IActorRequestMessage
    {
        private readonly IActorRequestMessageHeader header;
        private readonly IActorRequestMessageBody msgBody;

        public ActorRequestMessage(IActorRequestMessageHeader header, IActorRequestMessageBody msgBody)
        {
            this.header = header;
            this.msgBody = msgBody;
        }

        public IActorRequestMessageHeader GetHeader()
        {
            return this.header;
        }

        public IActorRequestMessageBody GetBody()
        {
            return this.msgBody;
        }
    }
}
