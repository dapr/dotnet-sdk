// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Dapr.Client;
using Grpc.Net.Client;
using GrpcServiceSample.Generated;

namespace GrpcProxy;

public class GreetingClient
{
    private readonly Greeter.GreeterClient client;

    public GreetingClient(string? daprEndpoint = null, GrpcChannelOptions? channelOptions = null)
    {
        // Target the server app ID, using the caller's local Dapr sidecar endpoint.
        // Reuse the invoker and generated client for the lifetime of the application.
        var invoker = channelOptions == null
            ? DaprClient.CreateInvocationInvoker(appId: "grpcsample", daprEndpoint: daprEndpoint)
            : DaprClient.CreateInvocationInvoker(appId: "grpcsample", grpcChannelOptions: channelOptions, daprEndpoint: daprEndpoint);
        client = new Greeter.GreeterClient(invoker);
    }

    public async Task<string> SayHelloAsync(string name, CancellationToken cancellationToken = default)
    {
        var reply = await client.SayHelloAsync(
            new HelloRequest { Name = name },
            deadline: DateTime.UtcNow.AddSeconds(10),
            cancellationToken: cancellationToken);
        return reply.Message;
    }
}
