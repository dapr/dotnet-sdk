// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc.v1;
    using FluentAssertions;
    using Google.Protobuf;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Moq;
    using Xunit;

    public class InvokeBindingApiTest
    {
        [Fact]
        public async Task InvokeBindingAsync_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var task = daprClient.InvokeBindingAsync<InvokeRequest>("test", "create", invokeRequest);

            // Get Request and validate                     
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeBindingRequest>(entry.Request);
            request.Name.Should().Be("test");
            request.Metadata.Count.Should().Be(0);
            var json = request.Data.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json, daprClient.JsonSerializerOptions);
            typeFromRequest.RequestParameter.Should().Be("Hello ");
        }

        [Fact]
        public async Task InvokeBindingAsync_ValidateRequest_WithMetadata()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var task = daprClient.InvokeBindingAsync<InvokeRequest>("test", "create", invokeRequest, metadata);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeBindingRequest>(entry.Request);
            request.Name.Should().Be("test");
            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");
            var json = request.Data.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json, daprClient.JsonSerializerOptions);
            typeFromRequest.RequestParameter.Should().Be("Hello ");
        }

        [Fact]
        public async Task InvokeBindingAsync_WithNullPayload_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeBindingAsync<InvokeRequest>("test", "create", null);

            // Get Request and validate                     
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeBindingRequest>(entry.Request);
            request.Name.Should().Be("test");
            request.Metadata.Count.Should().Be(0);
            var json = request.Data.ToStringUtf8();
            Assert.Equal("null", json);
        }

        [Fact]
        public async Task InvokeBindingAsync_WithRequest_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var payload = new InvokeRequest() { RequestParameter = "Hello " };
            var request = new BindingRequest("test", "create")
            {
                Data = JsonSerializer.SerializeToUtf8Bytes(payload, daprClient.JsonSerializerOptions),
                Metadata = 
                {
                    { "key1", "value1" },
                    { "key2", "value2" }
                }
            };

            var task = daprClient.InvokeBindingAsync(request);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();

            var gRpcRequest = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeBindingRequest>(entry.Request);
            gRpcRequest.Name.Should().Be("test");
            gRpcRequest.Metadata.Count.Should().Be(2);
            gRpcRequest.Metadata.Keys.Contains("key1").Should().BeTrue();
            gRpcRequest.Metadata.Keys.Contains("key2").Should().BeTrue();
            gRpcRequest.Metadata["key1"].Should().Be("value1");
            gRpcRequest.Metadata["key2"].Should().Be("value2");

            var json = gRpcRequest.Data.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json, daprClient.JsonSerializerOptions);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            var gRpcResponse = new Autogen.Grpc.v1.InvokeBindingResponse()
            {
                Data = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(new Widget() { Color = "red", }, daprClient.JsonSerializerOptions)),
                Metadata = 
                {
                    { "anotherkey", "anothervalue" },
                }
            };
            var streamContent = await GrpcUtils.CreateResponseContent(gRpcResponse);
            entry.Completion.SetResult(GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent));

            var response = await task;
            Assert.Same(request, response.Request);
            Assert.Equal("red", JsonSerializer.Deserialize<Widget>(response.Data.Span, daprClient.JsonSerializerOptions).Color);
            Assert.Collection(
                response.Metadata, 
                kvp => 
                { 
                    Assert.Equal("anotherkey", kvp.Key); 
                    Assert.Equal("anothervalue", kvp.Value); 
                });
        }


        [Fact]
        public async Task InvokeBindingAsync_WithCancelledToken()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var task = daprClient.InvokeBindingAsync<InvokeRequest>("test", "create", invokeRequest, metadata, ct);

            await FluentActions.Awaiting(async () => await task)
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task InvokeBindingAsync_WrapsRpcException()
        {
            var client = new MockClient();

            var rpcStatus = new Status(StatusCode.Internal, "not gonna work");
            var rpcException = new RpcException(rpcStatus, new Metadata(), "not gonna work");

            client.Mock
                .Setup(m => m.InvokeBindingAsync(It.IsAny<Autogen.Grpc.v1.InvokeBindingRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<DaprException>(async () => 
            {
                await client.DaprClient.InvokeBindingAsync("test", "test", new InvokeRequest() { RequestParameter = "Hello " });
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        [Fact]
        public async Task InvokeBindingAsync_WrapsJsonException()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var response = new Autogen.Grpc.v1.InvokeBindingResponse();
            var bytes = JsonSerializer.SerializeToUtf8Bytes<Widget>(new Widget(){ Color = "red", }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            response.Data = ByteString.CopyFrom(bytes.Take(10).ToArray()); // trim it to make invalid JSON blog

            var task = daprClient.InvokeBindingAsync<InvokeRequest, Widget>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeBindingRequest>(entry.Request);

            var streamContent = await GrpcUtils.CreateResponseContent(response);
            entry.Completion.SetResult(GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent));

            var ex = await Assert.ThrowsAsync<DaprException>(async () => 
            {
                await task;
            });
            Assert.IsType<JsonException>(ex.InnerException);
        }

        private class InvokeRequest
        {
            public string RequestParameter { get; set; }
        }

        private class Widget
        {
            public string Color { get; set; }
        }
    }
}
