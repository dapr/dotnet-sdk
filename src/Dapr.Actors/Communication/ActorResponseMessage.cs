// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Communication;

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