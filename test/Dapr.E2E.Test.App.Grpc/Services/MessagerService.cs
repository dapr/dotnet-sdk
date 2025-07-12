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

using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Dapr.E2E.Test;

public class MessagerService : Messager.MessagerBase
{
    private readonly MessageRepository repository;

    public MessagerService(MessageRepository repository)
    {
        this.repository = repository;
    }

    public override Task<Empty> SendMessage(SendMessageRequest request, ServerCallContext context)
    {
        this.repository.AddMessage(request.Recipient, request.Message);
        return Task.FromResult(new Empty());
    }

    public override Task<MessageResponse> GetMessage(GetMessageRequest request, ServerCallContext context)
    {
        return Task.FromResult(new MessageResponse { Message = this.repository.GetMessage(request.Recipient) });
    }

    public override async Task StreamBroadcast(IAsyncStreamReader<Broadcast> requestStream, IServerStreamWriter<MessageResponse> responseStream, ServerCallContext context)
    {
        await foreach(var request in requestStream.ReadAllAsync())
        {
            await responseStream.WriteAsync(new MessageResponse { Message = request.Message });
        }
    }

    public override async Task<Empty> DelayedResponse(Empty request, ServerCallContext context)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        return new Empty();
    }
}