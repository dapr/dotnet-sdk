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

using System.Threading.Tasks;
using Grpc.Core;
using GrpcServiceSample.Generated;

namespace GrpcServiceSample.Services;

public class GreeterService: Greeter.GreeterBase
{
    public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        await Task.Delay(1);
        return new HelloReply { Message = "Hello " + request.Name };
    }

    public override async Task SayHelloStream(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        await foreach (var message in requestStream.ReadAllAsync())
        {
            await responseStream.WriteAsync(new HelloReply() { Message = "Hello " + message.Name });
        }
    }
}
