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
using Dapr.Client.Autogen.Grpc.v1;
using Dapr.Client.Autogen.Test.Grpc.v1;
using Shouldly;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;
using Xunit;

using Request = Dapr.Client.Autogen.Test.Grpc.v1.Request;
using Response = Dapr.Client.Autogen.Test.Grpc.v1.Response;

namespace Dapr.Client.Test
{
    public partial class DaprClientTest
    {
        [Fact]
        public async Task InvokeMethodGrpcAsync_WithCancelledToken()
        {
            await using var client = TestClient.CreateForDaprClient(c =>
            {
                c.UseJsonSerializationOptions(this.jsonSerializerOptions);
            });

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await client.InnerClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " }, cancellationToken: cts.Token);
            });
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_CanInvokeMethodWithReturnTypeAndData()
        {
            await using var client = TestClient.CreateForDaprClient(c =>
            {
                c.UseJsonSerializationOptions(this.jsonSerializerOptions);
            });

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " });
            });

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<InvokeServiceRequest>();
            envelope.Id.ShouldBe("test");
            envelope.Message.Method.ShouldBe("test");
            envelope.Message.ContentType.ShouldBe(Constants.ContentTypeApplicationGrpc);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            var response = new Autogen.Grpc.v1.InvokeResponse()
            {
                Data = Any.Pack(data),
            };

            // Validate Response
            var invokedResponse = await request.CompleteWithMessageAsync(response);
            invokedResponse.Name.ShouldBe("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_CanInvokeMethodWithReturnTypeAndData_ThrowsExceptionForNonSuccess()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = Any.Pack(data),
            };

            await client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();

            const string rpcExceptionMessage = "RPC exception";
            const StatusCode rpcStatusCode = StatusCode.Unavailable;
            const string rpcStatusDetail = "Non success";

            var rpcStatus = new Status(rpcStatusCode, rpcStatusDetail);
            var rpcException = new RpcException(rpcStatus, new Metadata(), rpcExceptionMessage);

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<InvocationException>(async () =>
            {
                await client.DaprClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " });
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_CanInvokeMethodWithReturnTypeNoData()
        {
            await using var client = TestClient.CreateForDaprClient(c =>
            {
                c.UseJsonSerializationOptions(this.jsonSerializerOptions);
            });

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.InvokeMethodGrpcAsync<Response>("test", "test");
            });

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<InvokeServiceRequest>();
            envelope.Id.ShouldBe("test");
            envelope.Message.Method.ShouldBe("test");
            envelope.Message.ContentType.ShouldBe(string.Empty);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            var response = new Autogen.Grpc.v1.InvokeResponse()
            {
                Data = Any.Pack(data),
            };

            // Validate Response
            var invokedResponse = await request.CompleteWithMessageAsync(response);
            invokedResponse.Name.ShouldBe("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_CanInvokeMethodWithReturnTypeNoData_ThrowsExceptionNonSuccess()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = Any.Pack(data),
            };

            await client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();


            const string rpcExceptionMessage = "RPC exception";
            const StatusCode rpcStatusCode = StatusCode.Unavailable;
            const string rpcStatusDetail = "Non success";

            var rpcStatus = new Status(rpcStatusCode, rpcStatusDetail);
            var rpcException = new RpcException(rpcStatus, new Metadata(), rpcExceptionMessage);

            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<InvocationException>(async () =>
            {
                await client.DaprClient.InvokeMethodGrpcAsync<Response>("test", "test");
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_CanInvokeMethodWithNoReturnTypeAndData()
        {
            var request = new Request() { RequestParameter = "Hello " };
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = Any.Pack(data),
            };

            var response =
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();

            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Returns(response);

            await Should.NotThrowAsync(async () => await client.DaprClient.InvokeMethodGrpcAsync<Request>("test", "test", request));
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_CanInvokeMethodWithNoReturnTypeAndData_ThrowsErrorNonSuccess()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = Any.Pack(data),
            };

            await client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();


            const string rpcExceptionMessage = "RPC exception";
            const StatusCode rpcStatusCode = StatusCode.Unavailable;
            const string rpcStatusDetail = "Non success";

            var rpcStatus = new Status(rpcStatusCode, rpcStatusDetail);
            var rpcException = new RpcException(rpcStatus, new Metadata(), rpcExceptionMessage);

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<InvocationException>(async () =>
            {
                await client.DaprClient.InvokeMethodGrpcAsync<Request>("test", "test", new Request() { RequestParameter = "Hello " });
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_WithNoReturnTypeAndData()
        {
            await using var client = TestClient.CreateForDaprClient(c =>
            {
                c.UseJsonSerializationOptions(this.jsonSerializerOptions);
            });

            var invokeRequest = new Request() { RequestParameter = "Hello" };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", invokeRequest);
            });

            request.Dismiss();

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<InvokeServiceRequest>();
            envelope.Id.ShouldBe("test");
            envelope.Message.Method.ShouldBe("test");
            envelope.Message.ContentType.ShouldBe(Constants.ContentTypeApplicationGrpc);

            var actual = envelope.Message.Data.Unpack<Request>();
            Assert.Equal(invokeRequest.RequestParameter, actual.RequestParameter);
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_WithReturnTypeAndData()
        {
            await using var client = TestClient.CreateForDaprClient(c =>
            {
                c.UseJsonSerializationOptions(this.jsonSerializerOptions);
            });

            var invokeRequest = new Request() { RequestParameter = "Hello " };
            var invokeResponse = new Response { Name = "Look, I was invoked!" };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", invokeRequest);
            });

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<InvokeServiceRequest>();
            envelope.Id.ShouldBe("test");
            envelope.Message.Method.ShouldBe("test");
            envelope.Message.ContentType.ShouldBe(Constants.ContentTypeApplicationGrpc);

            var actual = envelope.Message.Data.Unpack<Request>();
            Assert.Equal(invokeRequest.RequestParameter, actual.RequestParameter);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            var response = new Autogen.Grpc.v1.InvokeResponse()
            {
                Data = Any.Pack(data),
            };

            // Validate Response
            var invokedResponse = await request.CompleteWithMessageAsync(response);
            invokedResponse.Name.ShouldBe(invokeResponse.Name);
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_AppCallback_SayHello()
        {
            // Configure Client
            var httpClient = new AppCallbackClient(new DaprAppCallbackService());
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions() { HttpClient = httpClient, })
                .UseJsonSerializationOptions(this.jsonSerializerOptions)
                .Build();

            var request = new Request() { RequestParameter = "Look, I was invoked!" };

            var response = await daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "SayHello", request);

            response.Name.ShouldBe("Hello Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_AppCallback_RepeatedField()
        {
            // Configure Client
            var httpClient = new AppCallbackClient(new DaprAppCallbackService());
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions() { HttpClient = httpClient, })
                .UseJsonSerializationOptions(this.jsonSerializerOptions)
                .Build();

            var testRun = new TestRun();
            testRun.Tests.Add(new TestCase() { Name = "test1" });
            testRun.Tests.Add(new TestCase() { Name = "test2" });
            testRun.Tests.Add(new TestCase() { Name = "test3" });

            var response = await daprClient.InvokeMethodGrpcAsync<TestRun, TestRun>("test", "TestRun", testRun);

            response.Tests.Count.ShouldBe(3);
            response.Tests[0].Name.ShouldBe("test1");
            response.Tests[1].Name.ShouldBe("test2");
            response.Tests[2].Name.ShouldBe("test3");
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_AppCallback_UnexpectedMethod()
        {
            // Configure Client
            var httpClient = new AppCallbackClient(new DaprAppCallbackService());
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions() { HttpClient = httpClient, })
                .UseJsonSerializationOptions(this.jsonSerializerOptions)
                .Build();

            var request = new Request() { RequestParameter = "Look, I was invoked!" };

            var response = await daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "not-existing", request);

            response.Name.ShouldBe("unexpected");
        }


        [Fact]
        public async Task GetMetadataAsync_WrapsRpcException()
        {
            var client = new MockClient();

            const string rpcExceptionMessage = "RPC exception";
            const StatusCode rpcStatusCode = StatusCode.Unavailable;
            const string rpcStatusDetail = "Non success";

            var rpcStatus = new Status(rpcStatusCode, rpcStatusDetail);
            var rpcException = new RpcException(rpcStatus, new Metadata(), rpcExceptionMessage);

            client.Mock
                .Setup(m => m.GetMetadataAsync(It.IsAny<GetMetadataRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<DaprException>(async () =>
            {
                await client.DaprClient.GetMetadataAsync(default);
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        [Fact]
        public async Task GetMetadataAsync_WithReturnTypeAndData()
        {
            await using var client = TestClient.CreateForDaprClient(c =>
            {
                c.UseJsonSerializationOptions(this.jsonSerializerOptions);
            });

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.GetMetadataAsync(default);
            });


            // Create Response & Respond
            var response = new Autogen.Grpc.v1.GetMetadataResponse()
            {
                ActorRuntime = new(),
                Id = "testId",
            };
            response.ActorRuntime.ActiveActors.Add(new ActiveActorsCount { Type = "testType", Count = 1 });
            response.RegisteredComponents.Add(new RegisteredComponents { Name = "testName", Type = "testType", Version = "V1" });
            response.ExtendedMetadata.Add("e1", "v1");

            // Validate Response
            var metadata = await request.CompleteWithMessageAsync(response);
            metadata.Id.ShouldBe("testId");
            metadata.Extended.ShouldContain(new System.Collections.Generic.KeyValuePair<string, string>("e1", "v1"));
            metadata.Actors.ShouldContain(actors => actors.Count == 1 && actors.Type == "testType");
            metadata.Components.ShouldContain(components => components.Name == "testName" && components.Type == "testType" && components.Version == "V1" && components.Capabilities.Length == 0);
        }

        [Fact]
        public async Task SetMetadataAsync_WrapsRpcException()
        {
            var client = new MockClient();

            const string rpcExceptionMessage = "RPC exception";
            const StatusCode rpcStatusCode = StatusCode.Unavailable;
            const string rpcStatusDetail = "Non success";

            var rpcStatus = new Status(rpcStatusCode, rpcStatusDetail);
            var rpcException = new RpcException(rpcStatus, new Metadata(), rpcExceptionMessage);

            client.Mock
                .Setup(m => m.SetMetadataAsync(It.IsAny<SetMetadataRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<DaprException>(async () =>
            {
                await client.DaprClient.SetMetadataAsync("testName", "", default);
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        [Fact]
        public async Task SetMetadataAsync_WithReturnTypeAndData()
        {
            await using var client = TestClient.CreateForDaprClient(c =>
            {
                c.UseJsonSerializationOptions(this.jsonSerializerOptions);
            });

            var request = await client.CaptureGrpcRequestAsync(daprClient =>
            {
                return daprClient.SetMetadataAsync("test", "testv", default);
            });

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<SetMetadataRequest>();
            envelope.Key.ShouldBe("test");
            envelope.Value.ShouldBe("testv");

            await request.CompleteWithMessageAsync(new Empty());

        }

        // Test implementation of the AppCallback.AppCallbackBase service
        private class DaprAppCallbackService : AppCallback.Autogen.Grpc.v1.AppCallback.AppCallbackBase
        {
            public override Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
            {
                return request.Method switch
                {
                    "SayHello" => SayHello(request),
                    "TestRun" => TestRun(request),
                    _ => Task.FromResult(new InvokeResponse()
                    {
                        Data = Any.Pack(new Response() { Name = $"unexpected" }),
                    }),
                };
            }

            private Task<InvokeResponse> SayHello(InvokeRequest request)
            {
                var helloRequest = request.Data.Unpack<Request>();
                var helloResponse = new Response() { Name = $"Hello {helloRequest.RequestParameter}" };

                return Task.FromResult(new InvokeResponse()
                {
                    Data = Any.Pack(helloResponse),
                });
            }

            private Task<InvokeResponse> TestRun(InvokeRequest request)
            {
                var echoRequest = request.Data.Unpack<TestRun>();
                return Task.FromResult(new InvokeResponse()
                {
                    Data = Any.Pack(echoRequest),
                });
            }
        }
    }
}
