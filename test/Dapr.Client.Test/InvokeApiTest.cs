// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.AppCallback.Autogen.Grpc.v1;
    using Dapr.Client;
    using Dapr.Client.Autogen.Grpc.v1;
    using Dapr.Client.Autogen.Test.Grpc.v1;
    using FluentAssertions;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Moq;
    using Xunit;

    public class InvokeApiTest
    {
        [Fact]
        public async Task InvokeMethodAsync_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var httpOptions = HttpInvocationOptions
                .UsingPost()
                .WithQueryParam("key1", "value1")
                .WithQueryParam("key2", "value2");

            var task = daprClient.InvokeMethodAsync<Response>("app1", "mymethod", httpOptions);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);

            envelope.Id.Should().Be("app1");
            envelope.Message.Method.Should().Be("mymethod");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);
            envelope.Message.HttpExtension.Verb.Should().Be(Autogen.Grpc.v1.HTTPExtension.Types.Verb.Post);
            envelope.Message.HttpExtension.Querystring.Count.Should().Be(2);
            envelope.Message.HttpExtension.Querystring.ContainsKey("key1").Should().BeTrue();
            envelope.Message.HttpExtension.Querystring.ContainsKey("key2").Should().BeTrue();
            envelope.Message.HttpExtension.Querystring["key1"].Should().Be("value1");
            envelope.Message.HttpExtension.Querystring["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task InvokeMethodAsync_WithCancelledToken()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();

            await FluentActions.Awaiting(async () => await daprClient.InvokeMethodAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " }, cancellationToken: ct))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_NoVerbSpecifiedByUser_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            // httpOptions not specified
            var task = daprClient.InvokeMethodAsync<Response>("app1", "mymethod");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);

            envelope.Id.Should().Be("app1");
            envelope.Message.Method.Should().Be("mymethod");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            envelope.Message.HttpExtension.Verb.Should().Be(Autogen.Grpc.v1.HTTPExtension.Types.Verb.Post);
            envelope.Message.HttpExtension.Querystring.Count.Should().Be(0);
        }

        [Fact]
        public void InvokeMethodAsync_HeadersSpecifiedByUser_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var httpOptions = HttpInvocationOptions
                .UsingPost()
                .WithHeader("Authorization", "Bearer foo")
                .WithHeader("X-Custom", "bar");

            var task = daprClient.InvokeMethodAsync<Response>("app1", "mymethod", httpOptions);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();

            entry.Request.Headers.Authorization.Scheme.Should().Be("Bearer");
            entry.Request.Headers.Authorization.Parameter.Should().Be("foo");
            entry.Request.Headers.GetValues("X-Custom").FirstOrDefault().Should().Be("bar");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Response>("test", "test");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            await SendResponse(data, entry);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData_EmptyResponseReturnsNull()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " });

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<Request>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            // Create Response & Respond
            await SendResponse<Response>(null, entry);

            // Validate Response.
            var invokedResponse = await task;
            invokedResponse.Should().BeNull();
        }


        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData_ThrowsExceptionForNonSuccess()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
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

            await FluentActions.Awaiting(async () => await client.DaprClient.InvokeMethodAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " }))
                .Should().ThrowAsync<InvocationException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeNoData()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Response>("test", "test");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            await SendResponse(data, entry);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeNoData_ThrowsExceptionNonSuccess()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
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

            await FluentActions.Awaiting(async () => await client.DaprClient.InvokeMethodAsync<Response>("test", "test")).Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public void InvokeMethodAsync_CanInvokeMethodWithNoReturnTypeAndData()
        {
            Request request = new Request() { RequestParameter = "Hello " };
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();
            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Returns(response);

            FluentActions.Awaiting(async () => await client.DaprClient.InvokeMethodAsync<Request>("test", "test", request)).Should().NotThrow();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithNoReturnTypeAndData_ThrowsErrorNonSuccess()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
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

            await FluentActions.Awaiting(async () => await client.DaprClient.InvokeMethodAsync<Request>("test", "test", new Request() { RequestParameter = "Hello " }))
                .Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_WithNoReturnTypeAndData_UsesConfiguredJsonSerializerOptions()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var invokeRequest = new Request() { RequestParameter = "Hello" };
            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test", invokeRequest);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));
        }

        [Fact]
        public async Task InvokeMethodAsync_WithReturnTypeAndData_UsesConfiguredJsonSerializerOptions()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var invokeRequest = new Request() { RequestParameter = "Hello " };
            var invokedResponse = new Response { Name = "Look, I was invoked!" };

            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test", invokeRequest);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));

            await SendResponse(invokedResponse, entry, jsonOptions);
            var response = await task;

            response.Name.Should().Be(invokedResponse.Name);
        }

        [Fact]
        public async Task InvokeMethodAsync_WithReturnTypeAndData_WithNonDefaultVerb_WithQueryString_UsesConfiguredJsonSerializerOptions()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var invokeRequest = new Request() { RequestParameter = "Hello " };
            var invokedResponse = new Response { Name = "Look, I was invoked!" };
            var httpOptions = HttpInvocationOptions
                .UsingPut()
                .WithQueryParam("key1", "value1");

            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test1", invokeRequest, httpOptions);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test1");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);
            envelope.Message.HttpExtension.Verb.Should().Be(Autogen.Grpc.v1.HTTPExtension.Types.Verb.Put);
            envelope.Message.HttpExtension.Querystring.Count.Should().Be(1);
            envelope.Message.HttpExtension.Querystring.ContainsKey("key1").Should().BeTrue();
            envelope.Message.HttpExtension.Querystring["key1"].Should().Be("value1");


            var json = envelope.Message.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));

            await SendResponse(invokedResponse, entry, jsonOptions);
            var response = await task;

            response.Name.Should().Be(invokedResponse.Name);
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithResponse_CalleeSideGrpc()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();

            
            client.Mock
            .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
            .Returns(response);

            var body = new Request() { RequestParameter = "Hello " };
            var task = client.DaprClient.InvokeMethodWithResponseAsync<Request, Response>("test", "testMethod", body);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Body.Name.Should().Be("Look, I was invoked!");
            invokedResponse.Headers.Count.Should().Be(0);
            invokedResponse.ContentType.Should().Be(Constants.ContentTypeApplicationGrpc);
            invokedResponse.HttpStatusCode.Should().BeNull();
            invokedResponse.GrpcStatusInfo.Should().NotBeNull();
            invokedResponse.GrpcStatusInfo.GrpcStatusCode.Should().Be(Grpc.Core.StatusCode.OK);
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithResponse_CalleeSideHttp()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .AddHeader("dapr-http-status", "200")
                .Build();

            
            client.Mock
            .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
            .Returns(response);

            var body = new Request() { RequestParameter = "Hello " };
            var task = client.DaprClient.InvokeMethodWithResponseAsync<Request, Response>("test", "testMethod", body);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Body.Name.Should().Be("Look, I was invoked!");
            invokedResponse.Headers.ContainsKey("dapr-http-status").Should().BeTrue();
            invokedResponse.ContentType.Should().Be(Constants.ContentTypeApplicationJson);
            invokedResponse.GrpcStatusInfo.Should().BeNull();
            invokedResponse.HttpStatusCode.Should().NotBeNull();
            invokedResponse.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithResponse_GrpcServerReturnsNonSuccessResponse()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();


            var trailers = new Metadata();
             const string grpcErrorInfoReason = "Insufficient permissions";
            int grpcStatusCode = Convert.ToInt32(StatusCode.PermissionDenied);
            const string grpcStatusMessage = "Bad permissions";
            var details = new Google.Rpc.Status
            {
                Code = grpcStatusCode,
                Message = grpcStatusMessage,
            };

            var errorInfo = new Google.Rpc.ErrorInfo
            {
                Reason = grpcErrorInfoReason,
                Domain = "dapr.io",
            };
            details.Details.Add(Google.Protobuf.WellKnownTypes.Any.Pack(errorInfo));


            var entry = new Metadata.Entry("grpc-status-details-bin", Google.Protobuf.MessageExtensions.ToByteArray(details));
            trailers.Add(entry);

            const string rpcExceptionMessage = "No access to app";
            const StatusCode rpcStatusCode = StatusCode.PermissionDenied;
            const string rpcStatusDetail = "Insufficient permissions";

            var rpcException = new RpcException(new Status(rpcStatusCode, rpcStatusDetail), trailers, rpcExceptionMessage);


            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            try
            {
                var body = new Request() { RequestParameter = "Hello " };
                await client.DaprClient.InvokeMethodWithResponseAsync<Request, Response>("test", "testMethod", body);
                Assert.False(true);
            }
            catch(InvocationException ex)
            {
                ex.Message.Should().Be("Exception while invoking testMethod on appId:test");
                ex.InnerException.Message.Should().Be(rpcExceptionMessage);

                ex.Response.GrpcStatusInfo.GrpcErrorMessage.Should().Be(grpcStatusMessage);
                ex.Response.GrpcStatusInfo.GrpcStatusCode.Should().Be(grpcStatusCode);
                ex.Response.Body.Should().BeNull();
                ex.Response.HttpStatusCode.Should().BeNull();
            }
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithResponse_HttpServerReturnsNonSuccessResponse()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data),
                ContentType = Constants.ContentTypeApplicationJson
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .AddHeader("dapr-http-status", "200")
                .Build();


            var trailers = new Metadata();
             const string grpcErrorInfoReason = "Insufficient permissions";
            const int grpcErrorInfoDetailHttpCode = 500;
            const string grpcErrorInfoDetailHttpErrorMsg = "no permissions";
            int grpcStatusCode = Convert.ToInt32(StatusCode.PermissionDenied);
            const string grpcStatusMessage = "Bad permissions";
            var details = new Google.Rpc.Status
            {
                Code = grpcStatusCode,
                Message = grpcStatusMessage,
            };

            var errorInfo = new Google.Rpc.ErrorInfo
            {
                Reason = grpcErrorInfoReason,
                Domain = "dapr.io",
            };
            errorInfo.Metadata.Add("http.code", grpcErrorInfoDetailHttpCode.ToString());
            errorInfo.Metadata.Add("http.error_message", grpcErrorInfoDetailHttpErrorMsg);
            details.Details.Add(Google.Protobuf.WellKnownTypes.Any.Pack(errorInfo));


            var entry = new Metadata.Entry("grpc-status-details-bin", Google.Protobuf.MessageExtensions.ToByteArray(details));
            trailers.Add(entry);

            const string rpcExceptionMessage = "No access to app";
            const StatusCode rpcStatusCode = StatusCode.PermissionDenied;
            const string rpcStatusDetail = "Insufficient permissions";

            var rpcException = new RpcException(new Status(rpcStatusCode, rpcStatusDetail), trailers, rpcExceptionMessage);


            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            try
            {
                var body = new Request() { RequestParameter = "Hello " };
                await client.DaprClient.InvokeMethodWithResponseAsync<Request, Response>("test", "testMethod", body);
                Assert.False(true);
            }
            catch(InvocationException ex)
            {
                ex.Message.Should().Be("Exception while invoking testMethod on appId:test");
                ex.InnerException.Message.Should().Be(rpcExceptionMessage);

                ex.Response.GrpcStatusInfo.Should().BeNull();
                Encoding.UTF8.GetString(ex.Response.Body).Should().Be(grpcErrorInfoDetailHttpErrorMsg);
                ex.Response.HttpStatusCode.Should().Be(grpcErrorInfoDetailHttpCode);
            }
        }

        [Fact]
        public async Task InvokeMethodWithResponseAsync_WithCancelledToken()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();

            await FluentActions.Awaiting(async () => await daprClient.InvokeMethodWithResponseAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " }, cancellationToken: ct))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeRawMethodWithResponse_CalleeSideGrpc()
        {
            var client = new MockClient();
            var responseBody = new Response() { Name = "Look, I was invoked!" };
            // var dataBytes = new byte[]{1,2,3};
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(responseBody);
            var invokeResponse = new InvokeResponse
            {
                Data = new Any { Value = ByteString.CopyFrom(responseBytes), TypeUrl = typeof(byte[]).FullName }
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();

            var requestBody = new Request() { RequestParameter = "Hello " };
            var requestBytes = JsonSerializer.SerializeToUtf8Bytes(requestBody);
            
            client.Mock
            .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
            .Returns(response);

            var task = client.DaprClient.InvokeMethodRawAsync("test", "testMethod", requestBytes);

            // Validate Response
            var invokedResponse = await task;
            var invokedResponseBody = JsonSerializer.Deserialize<Response>(invokedResponse.Body);
            invokedResponseBody.Name.Should().Be("Look, I was invoked!");
            invokedResponse.HttpStatusCode.Should().BeNull();
            invokedResponse.GrpcStatusInfo.Should().NotBeNull();
            invokedResponse.GrpcStatusInfo.GrpcStatusCode.Should().Be(Grpc.Core.StatusCode.OK);
            invokedResponse.Headers.Count.Should().Be(0);
            invokedResponse.ContentType.Should().Be(Constants.ContentTypeApplicationGrpc);

            var expectedRequest = new Autogen.Grpc.v1.InvokeServiceRequest
            {
                Id = "test",
                Message = new InvokeRequest
                {
                    Method = "testMethod",
                    Data = new Any { Value = ByteString.CopyFrom(requestBytes), TypeUrl = typeof(byte[]).FullName },
                    HttpExtension = new Autogen.Grpc.v1.HTTPExtension
                    {
                        Verb = Autogen.Grpc.v1.HTTPExtension.Types.Verb.Post,
                    },
                    ContentType = Constants.ContentTypeApplicationJson,
                },
            };
            client.Mock.Verify(m => m.InvokeServiceAsync(It.Is<Autogen.Grpc.v1.InvokeServiceRequest>(request => request.Equals(expectedRequest)), It.IsAny<CallOptions>()));

        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeRawMethodWithResponse_CalleeSideHttp()
        {
            var client = new MockClient();
            var responseBody = new Response() { Name = "Look, I was invoked!" };
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(responseBody);
            var invokeResponse = new InvokeResponse
            {
                Data = new Any { Value = ByteString.CopyFrom(responseBytes), TypeUrl = typeof(byte[]).FullName }
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .AddHeader("dapr-http-status", "200")
                .Build();

            var requestBody = new Request() { RequestParameter = "Hello " };
            var requestBytes = JsonSerializer.SerializeToUtf8Bytes(requestBody);
            
            client.Mock
            .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
            .Returns(response);

            var task = client.DaprClient.InvokeMethodRawAsync("test", "testMethod", requestBytes);

            // Validate Response
            var invokedResponse = await task;
            var invokedResponseBody = JsonSerializer.Deserialize<Response>(invokedResponse.Body);
            invokedResponseBody.Name.Should().Be("Look, I was invoked!");
            invokedResponse.Headers.ContainsKey("dapr-http-status");
            invokedResponse.ContentType = Constants.ContentTypeApplicationJson;
            invokedResponse.GrpcStatusInfo.Should().BeNull();
            invokedResponse.HttpStatusCode.Should().NotBeNull();
            invokedResponse.HttpStatusCode.Should().Be(HttpStatusCode.OK);
            invokedResponse.Headers.ContainsKey("dapr-http-status").Should().BeTrue();
            invokedResponse.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var expectedRequest = new Autogen.Grpc.v1.InvokeServiceRequest
            {
                Id = "test",
                Message = new InvokeRequest
                {
                    Method = "testMethod",
                    Data = new Any { Value = ByteString.CopyFrom(requestBytes), TypeUrl = typeof(byte[]).FullName },
                    HttpExtension = new Autogen.Grpc.v1.HTTPExtension
                    {
                        Verb = Autogen.Grpc.v1.HTTPExtension.Types.Verb.Post,
                    },
                    ContentType = "application/json",
                },
            };
            client.Mock.Verify(m => m.InvokeServiceAsync(It.Is<Autogen.Grpc.v1.InvokeServiceRequest>(request => request.Equals(expectedRequest)), It.IsAny<CallOptions>()));
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeRawMethodWithResponse_GrpcServerReturnsNonSuccessResponse()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .Build();

            var trailers = new Metadata();
             const string grpcErrorInfoReason = "Insufficient permissions";
            int grpcStatusCode = Convert.ToInt32(StatusCode.PermissionDenied);
            const string grpcStatusMessage = "Bad permissions";
            var details = new Google.Rpc.Status
            {
                Code = grpcStatusCode,
                Message = grpcStatusMessage,
            };

            var errorInfo = new Google.Rpc.ErrorInfo
            {
                Reason = grpcErrorInfoReason,
                Domain = "dapr.io",
            };
            details.Details.Add(Google.Protobuf.WellKnownTypes.Any.Pack(errorInfo));


            var entry = new Metadata.Entry("grpc-status-details-bin", Google.Protobuf.MessageExtensions.ToByteArray(details));
            trailers.Add(entry);

            const string rpcExceptionMessage = "No access to app";
            const StatusCode rpcStatusCode = StatusCode.PermissionDenied;
            const string rpcStatusDetail = "Insufficient permissions";

            var rpcException = new RpcException(new Status(rpcStatusCode, rpcStatusDetail), trailers, rpcExceptionMessage);


            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);


            try
            {
                var body = new Request() { RequestParameter = "Hello " };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
                await client.DaprClient.InvokeMethodRawAsync("test", "testMethod", bytes);
                Assert.False(true);
            }
            catch(InvocationException ex)
            {
                ex.Message.Should().Be("Exception while invoking testMethod on appId:test");
                ex.InnerException.Message.Should().Be(rpcExceptionMessage);

                ex.Response.GrpcStatusInfo.GrpcErrorMessage.Should().Be(grpcStatusMessage);
                ex.Response.GrpcStatusInfo.GrpcStatusCode.Should().Be(grpcStatusCode);
                ex.Response.Body.Should().BeNull();
                ex.Response.HttpStatusCode.Should().BeNull();
            }
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeRawMethodWithResponse_HttpServerReturnsNonSuccessResponse()
        {
            var client = new MockClient();
            var data = new Response() { Name = "Look, I was invoked!" };
            var invokeResponse = new InvokeResponse
            {
                Data = TypeConverters.ToAny(data)
            };

            var response = 
                client.Call<InvokeResponse>()
                .SetResponse(invokeResponse)
                .AddHeader("dapr-status-header", "200")
                .Build();

            var trailers = new Metadata();
             const string grpcErrorInfoReason = "Insufficient permissions";
            const int grpcErrorInfoDetailHttpCode = 500;
            const string grpcErrorInfoDetailHttpErrorMsg = "no permissions";
            int grpcStatusCode = Convert.ToInt32(StatusCode.PermissionDenied);
            const string grpcStatusMessage = "Bad permissions";
            var details = new Google.Rpc.Status
            {
                Code = grpcStatusCode,
                Message = grpcStatusMessage,
            };

            var errorInfo = new Google.Rpc.ErrorInfo
            {
                Reason = grpcErrorInfoReason,
                Domain = "dapr.io",
            };
            errorInfo.Metadata.Add("http.code", grpcErrorInfoDetailHttpCode.ToString());
            errorInfo.Metadata.Add("http.error_message", grpcErrorInfoDetailHttpErrorMsg);
            details.Details.Add(Google.Protobuf.WellKnownTypes.Any.Pack(errorInfo));


            var entry = new Metadata.Entry("grpc-status-details-bin", Google.Protobuf.MessageExtensions.ToByteArray(details));
            trailers.Add(entry);

            const string rpcExceptionMessage = "No access to app";
            const StatusCode rpcStatusCode = StatusCode.PermissionDenied;
            const string rpcStatusDetail = "Insufficient permissions";

            var rpcException = new RpcException(new Status(rpcStatusCode, rpcStatusDetail), trailers, rpcExceptionMessage);


            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.InvokeServiceAsync(It.IsAny<Autogen.Grpc.v1.InvokeServiceRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);


            try
            {
                var body = new Request() { RequestParameter = "Hello " };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
                await client.DaprClient.InvokeMethodRawAsync("test", "testMethod", bytes);
                Assert.False(true);
            }
            catch(InvocationException ex)
            {
                ex.Message.Should().Be("Exception while invoking testMethod on appId:test");
                ex.InnerException.Message.Should().Be(rpcExceptionMessage);

                ex.Response.GrpcStatusInfo.Should().BeNull();
                Encoding.UTF8.GetString(ex.Response.Body).Should().Be(grpcErrorInfoDetailHttpErrorMsg);
                ex.Response.HttpStatusCode.Should().Be(grpcErrorInfoDetailHttpCode);
            }
        }

        [Fact]
        public async Task InvokeRawMethodAsync_WithCancelledToken()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();

            var body = new Request() { RequestParameter = "Hello " };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
            await FluentActions.Awaiting(async () => await daprClient.InvokeMethodRawAsync("test", "testMethod", bytes, cancellationToken: ct))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_AppCallback_SayHello()
        {
            // Configure Client
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var httpClient = new AppCallbackClient(new DaprAppCallbackService(jsonOptions));
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var request = new Request() { RequestParameter = "Look, I was invoked!" };

            var response = await daprClient.InvokeMethodAsync<Request, Response>("test", "SayHello", request);

            response.Name.Should().Be("Hello Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_AppCallback_RepeatedField()
        {
            // Configure Client
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var httpClient = new AppCallbackClient(new DaprAppCallbackService(jsonOptions));
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var testRun = new TestRun();
            testRun.Tests.Add(new TestCase() { Name = "test1" });
            testRun.Tests.Add(new TestCase() { Name = "test2" });
            testRun.Tests.Add(new TestCase() { Name = "test3" });

            var response = await daprClient.InvokeMethodAsync<TestRun, TestRun>("test", "TestRun", testRun);

            response.Tests.Count.Should().Be(3);
            response.Tests[0].Name.Should().Be("test1");
            response.Tests[1].Name.Should().Be("test2");
            response.Tests[2].Name.Should().Be("test3");
        }

        [Fact]
        public async Task InvokeMethodAsync_AppCallback_UnexpectedMethod()
        {
            // Configure Client
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var httpClient = new AppCallbackClient(new DaprAppCallbackService(jsonOptions));
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var request = new Request() { RequestParameter = "Look, I was invoked!" };

            var response = await daprClient.InvokeMethodAsync<Request, Response>("test", "not-existing", request);

            response.Name.Should().Be("unexpected");
        }

        private async Task SendResponse<T>(T data, TestHttpClient.Entry entry, JsonSerializerOptions options = null)
        {
            var dataAny = TypeConverters.ToAny(data, options);
            var dataResponse = new InvokeResponse
            {
                Data = dataAny
            };

            var streamContent = await GrpcUtils.CreateResponseContent(dataResponse);
            var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            entry.Completion.SetResult(response);
        }

        private class Request
        {
            public string RequestParameter { get; set; }
        }

        private class Response
        {
            public string Name { get; set; }
        }

        // Test implementation of the AppCallback.AppCallbackBase service
        private class DaprAppCallbackService : AppCallback.AppCallbackBase
        {
            private readonly JsonSerializerOptions jsonOptions;

            public DaprAppCallbackService(JsonSerializerOptions jsonOptions)
            {
                this.jsonOptions = jsonOptions;
            }

            public override Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
            {
                return request.Method switch
                {
                    "SayHello" => SayHello(request),
                    "TestRun" => TestRun(request),
                    _ => Task.FromResult(new InvokeResponse()
                    {
                        Data = TypeConverters.ToAny(new Response() { Name = $"unexpected" }, this.jsonOptions)
                    })
                };
            }

            private Task<InvokeResponse> SayHello(InvokeRequest request)
            {
                var helloRequest = TypeConverters.FromAny<Request>(request.Data, this.jsonOptions);
                var helloResponse = new Response() { Name = $"Hello {helloRequest.RequestParameter}" };

                return Task.FromResult(new InvokeResponse()
                {
                    Data = TypeConverters.ToAny(helloResponse, this.jsonOptions)
                });
            }

            private Task<InvokeResponse> TestRun(InvokeRequest request)
            {
                var echoRequest = TypeConverters.FromAny<TestRun>(request.Data, this.jsonOptions);

                return Task.FromResult(new InvokeResponse()
                {
                    Data = TypeConverters.ToAny(echoRequest, this.jsonOptions)
                });
            }
        }
    }
}
