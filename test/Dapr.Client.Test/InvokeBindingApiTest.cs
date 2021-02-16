// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc.v1;
    using FluentAssertions;
    using Google.Protobuf;
    using Grpc.Core;
    using Moq;
    using Xunit;

    public class InvokeBindingApiTest
    {
        [Fact]
        public async Task InvokeBindingAsync_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprClient();

            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                await daprClient.InvokeBindingAsync<InvokeRequest>("test", "create", invokeRequest);
            });

            request.Dismiss();

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<InvokeBindingRequest>();
            envelope.Name.Should().Be("test");
            envelope.Metadata.Count.Should().Be(0);
            var json = envelope.Data.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json, client.InnerClient.JsonSerializerOptions);
            typeFromRequest.RequestParameter.Should().Be("Hello ");
        }

        [Fact]
        public async Task InvokeBindingAsync_ValidateRequest_WithMetadata()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                await daprClient.InvokeBindingAsync<InvokeRequest>("test", "create", invokeRequest, metadata);
            });

            request.Dismiss();

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<InvokeBindingRequest>();
            envelope.Name.Should().Be("test");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");
            var json = envelope.Data.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json, client.InnerClient.JsonSerializerOptions);
            typeFromRequest.RequestParameter.Should().Be("Hello ");
        }

        [Fact]
        public async Task InvokeBindingAsync_WithNullPayload_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprClient();

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                await daprClient.InvokeBindingAsync<InvokeRequest>("test", "create", null);
            });

            request.Dismiss();

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<InvokeBindingRequest>();
            envelope.Name.Should().Be("test");
            envelope.Metadata.Count.Should().Be(0);
            var json = envelope.Data.ToStringUtf8();
            Assert.Equal("null", json);
        }

        [Fact]
        public async Task InvokeBindingAsync_WithRequest_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprClient();

            var payload = new InvokeRequest() { RequestParameter = "Hello " };
            var bindingRequest = new BindingRequest("test", "create")
            {
                Data = JsonSerializer.SerializeToUtf8Bytes(payload, client.InnerClient.JsonSerializerOptions),
                Metadata = 
                {
                    { "key1", "value1" },
                    { "key2", "value2" }
                }
            };

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.InvokeBindingAsync(bindingRequest);
            });

            var gRpcResponse = new Autogen.Grpc.v1.InvokeBindingResponse()
            {
                Data = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(new Widget() { Color = "red", }, client.InnerClient.JsonSerializerOptions)),
                Metadata = 
                {
                    { "anotherkey", "anothervalue" },
                }
            };
            var response = await request.CompleteWithMessageAsync(gRpcResponse);

            var envelope = await request.GetRequestEnvelopeAsync<InvokeBindingRequest>();
            envelope.Name.Should().Be("test");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");

            var json = envelope.Data.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json, client.InnerClient.JsonSerializerOptions);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            Assert.Same(bindingRequest, response.Request);
            Assert.Equal("red", JsonSerializer.Deserialize<Widget>(response.Data.Span, client.InnerClient.JsonSerializerOptions).Color);
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
            await using var client = TestClient.CreateForDaprClient();

            var cts = new CancellationTokenSource();
            cts.Cancel();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await client.InnerClient.InvokeBindingAsync<InvokeRequest>("test", "create", invokeRequest, metadata, cts.Token);
            });
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
            await using var client = TestClient.CreateForDaprClient();

            var response = new Autogen.Grpc.v1.InvokeBindingResponse();
            var bytes = JsonSerializer.SerializeToUtf8Bytes<Widget>(new Widget(){ Color = "red", }, client.InnerClient.JsonSerializerOptions);
            response.Data = ByteString.CopyFrom(bytes.Take(10).ToArray()); // trim it to make invalid JSON blob

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.InvokeBindingAsync<InvokeRequest, Widget>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });
            });

            var envelope = await request.GetRequestEnvelopeAsync<InvokeBindingRequest>();
            var ex = await Assert.ThrowsAsync<DaprException>(async () => 
            {
                await request.CompleteWithMessageAsync(response);
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
