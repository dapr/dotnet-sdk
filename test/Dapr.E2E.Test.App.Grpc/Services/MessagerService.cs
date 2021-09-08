// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation and Dapr Contributors.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Dapr.E2E.Test
{
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
    }
}