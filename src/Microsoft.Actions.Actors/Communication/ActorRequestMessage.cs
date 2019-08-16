// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    internal class ActorRequestMessage : IActorRequestMessage
    {
        private readonly IActorRequestMessageHeader header;
        private readonly IActorMessageBody msgBody;

        public ActorRequestMessage(IActorRequestMessageHeader header, IActorMessageBody msgBody)
        {
            this.header = header;
            this.msgBody = msgBody;
        }

        public IActorRequestMessageHeader GetHeader()
        {
            return this.header;
        }

        public IActorMessageBody GetBody()
        {
            return this.msgBody;
        }
    }
}
