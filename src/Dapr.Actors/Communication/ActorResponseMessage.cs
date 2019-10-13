// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    internal class ActorResponseMessage : IActorResponseMessage
    {
        private readonly IActorResponseMessageHeader header;
        private readonly IActorResponseMessageBody msgBody;

        public ActorResponseMessage(
            IActorResponseMessageHeader header,
            IActorResponseMessageBody msgBody)
        {
            this.header = header;
            this.msgBody = msgBody;
        }

        public IActorResponseMessageHeader GetHeader()
        {
            return this.header;
        }

        public IActorResponseMessageBody GetBody()
        {
            return this.msgBody;
        }
    }
}
