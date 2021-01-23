﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client.Autogen.Grpc.v1;
using Dapr.Client.Autogen.Test.Grpc.v1;
using FluentAssertions;
using Google.Protobuf;
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
        private DaprClient CreateTestClientGrpc(HttpClient httpClient)
        {
            return new DaprClientBuilder()
                .UseJsonSerializationOptions(this.jsonSerializerOptions)
                .UseGrpcChannelOptions(new GrpcChannelOptions 
                { 
                    HttpClient = httpClient, 
                    ThrowOperationCanceledOnCancellation = true,
                })
                .Build();
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_WithCancelledToken()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = CreateTestClientGrpc(httpClient);

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();

            await FluentActions.Awaiting(async () => 
            {
                await daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " }, cancellationToken: ct);
            }).Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_CanInvokeMethodWithReturnTypeAndData()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = CreateTestClientGrpc(httpClient);

            var task = daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " });

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationGrpc);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            await SendResponse(data, entry);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
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

            var response = 
                client.Call<InvokeResponse>()
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
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = CreateTestClientGrpc(httpClient);

            var task = daprClient.InvokeMethodGrpcAsync<Response>("test", "test");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(string.Empty);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            await SendResponse(data, entry);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
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

            var response = 
                client.Call<InvokeResponse>()
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
        public void InvokeMethodGrpcAsync_CanInvokeMethodWithNoReturnTypeAndData()
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

            FluentActions.Awaiting(async () => await client.DaprClient.InvokeMethodGrpcAsync<Request>("test", "test", request)).Should().NotThrow();
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

            var response = 
                client.Call<InvokeResponse>()
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
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = CreateTestClientGrpc(httpClient);

            var invokeRequest = new Request() { RequestParameter = "Hello" };
            var task = daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", invokeRequest);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationGrpc);
            
            var actual = envelope.Message.Data.Unpack<Request>();
            Assert.Equal(invokeRequest.RequestParameter, actual.RequestParameter);
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_WithReturnTypeAndData()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = CreateTestClientGrpc(httpClient);

            var invokeRequest = new Request() { RequestParameter = "Hello " };
            var invokedResponse = new Response { Name = "Look, I was invoked!" };

            var task = daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "test", invokeRequest);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationGrpc);

            var actual = envelope.Message.Data.Unpack<Request>();
            Assert.Equal(invokeRequest.RequestParameter, actual.RequestParameter);

            await SendResponse(invokedResponse, entry);
            var response = await task;

            response.Name.Should().Be(invokedResponse.Name);
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_AppCallback_SayHello()
        {
            // Configure Client
            var httpClient = new AppCallbackClient(new DaprAppCallbackService());
            var daprClient = CreateTestClientGrpc(httpClient);

            var request = new Request() { RequestParameter = "Look, I was invoked!" };

            var response = await daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "SayHello", request);

            response.Name.Should().Be("Hello Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_AppCallback_RepeatedField()
        {
            // Configure Client
            var httpClient = new AppCallbackClient(new DaprAppCallbackService());
            var daprClient = CreateTestClientGrpc(httpClient);

            var testRun = new TestRun();
            testRun.Tests.Add(new TestCase() { Name = "test1" });
            testRun.Tests.Add(new TestCase() { Name = "test2" });
            testRun.Tests.Add(new TestCase() { Name = "test3" });

            var response = await daprClient.InvokeMethodGrpcAsync<TestRun, TestRun>("test", "TestRun", testRun);

            response.Tests.Count.Should().Be(3);
            response.Tests[0].Name.Should().Be("test1");
            response.Tests[1].Name.Should().Be("test2");
            response.Tests[2].Name.Should().Be("test3");
        }

        [Fact]
        public async Task InvokeMethodGrpcAsync_AppCallback_UnexpectedMethod()
        {
            // Configure Client
            var httpClient = new AppCallbackClient(new DaprAppCallbackService());
            var daprClient = CreateTestClientGrpc(httpClient);

            var request = new Request() { RequestParameter = "Look, I was invoked!" };

            var response = await daprClient.InvokeMethodGrpcAsync<Request, Response>("test", "not-existing", request);

            response.Name.Should().Be("unexpected");
        }


        private async Task SendResponse<T>(T data, TestHttpClient.Entry entry) where T : IMessage
        {
            var dataResponse = new InvokeResponse
            {
                Data = Any.Pack(data),
            };

            var streamContent = await GrpcUtils.CreateResponseContent(dataResponse);
            var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            entry.Completion.SetResult(response);
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
