// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
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
