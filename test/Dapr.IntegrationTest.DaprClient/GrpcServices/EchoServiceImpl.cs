// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Dapr.IntegrationTest.DaprClient.GrpcServices;

/// <summary>
/// In-process gRPC service implementation used by the gRPC proxy integration tests.
/// </summary>
public sealed class EchoServiceImpl : EchoService.EchoServiceBase
{
    /// <summary>
    /// Unary echo: returns the same message that was sent.
    /// </summary>
    public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
        => Task.FromResult(new EchoReply { Message = request.Message });

    /// <summary>
    /// Bidirectional-streaming echo: reflects every incoming message back to the caller.
    /// </summary>
    public override async Task BidirectionalEcho(
        IAsyncStreamReader<EchoRequest> requestStream,
        IServerStreamWriter<EchoReply> responseStream,
        ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(new EchoReply { Message = request.Message }, context.CancellationToken);
        }
    }

    /// <summary>
    /// Slow echo: deliberately delays the response to allow deadline / timeout tests.
    /// </summary>
    public override async Task<Empty> SlowEcho(Empty request, ServerCallContext context)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);
        return new Empty();
    }
}
