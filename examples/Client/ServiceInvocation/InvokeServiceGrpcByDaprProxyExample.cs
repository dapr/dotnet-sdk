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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using Grpc.Net.Client;
using GrpcServiceSample.Generated;

namespace Samples.Client
{
    public class InvokeServiceGrpcByDaprProxyExample : Example
    {
        public override string DisplayName => "Invoking a existing proto-based gRPC service with DaprClient";
        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("** use sdk created client **");
            await UseSdkCreateClient(cancellationToken);
            Console.WriteLine("** use sdk created client completed **");
            
            Console.WriteLine("** use grpc client **");
            await UseSdkCreateClient(cancellationToken);
            Console.WriteLine("** use grpc client completed **");
        }

        private async Task UseSdkCreateClient(CancellationToken cancellationToken)
        {
            var invoker = DaprClient.CreateInvocationInvoker("grpcsample");
            var client = new Greeter.GreeterClient(invoker);

            await InvokeExample(client, cancellationToken);
        }
        private async Task UseGrpcClientOnly(CancellationToken cancellationToken)
        {
            var daprEndpoint = Environment.GetEnvironmentVariable("DAPR_GRPC_ENDPOINT");
            if (string.IsNullOrEmpty(daprEndpoint))
            {
                throw new InvalidOperationException("Dapr endpoint not set");
            }
            var channel = GrpcChannel.ForAddress(daprEndpoint);
            var client = new Greeter.GreeterClient(channel);
            
            await InvokeExample(client, cancellationToken);
        }
        private static async Task InvokeExample(Greeter.GreeterClient client, CancellationToken cancellationToken)
        {

            Console.WriteLine("Invoking grpc SayHello");
            var helloRequest = new HelloRequest { Name = "a request through dapr" };
            var helloReply = await client.SayHelloAsync(helloRequest, cancellationToken: cancellationToken);
            Console.WriteLine("Returned: message:{0}", helloReply.Message);
            Console.WriteLine("Completed grpc SayHello");

            Console.WriteLine("Invoking grpc Bi-directional streaming SayHelloStream");
            var helloStream = client.SayHelloStream(cancellationToken: cancellationToken);
            for (int i = 0; i < 5; i++)
            {
                var helloStreamRequest = new HelloRequest { Name = $"a request through dapr {i}" };
                await helloStream.RequestStream.WriteAsync(helloStreamRequest);
                if (!await helloStream.ResponseStream.MoveNext(cancellationToken))
                {
                    break;
                }
                Console.WriteLine("Returned: message:{0}", helloStream.ResponseStream.Current.Message);
            }
            Console.WriteLine("Completed grpc Bi-directional streaming SayHelloStream");
        }

        
    }
}
