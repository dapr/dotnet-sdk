// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc.v1;
    using FluentAssertions;
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

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");
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
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");
        }

        [Fact]
        public async Task InvokeBindingAsync_WithCancelledToken()
        {
            // Configure Client
            var client = new MockClient();
            var response =
                client.InvokeBinding<InvokeResponse>()
                .Build();

            const string rpcExceptionMessage = "Call canceled by client";
            const StatusCode rpcStatusCode = StatusCode.Cancelled;
            const string rpcStatusDetail = "Call canceled";

            var rpcStatus = new Status(rpcStatusCode, rpcStatusDetail);
            var rpcException = new RpcException(rpcStatus, new Metadata(), rpcExceptionMessage);

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.InvokeBindingAsync(It.IsAny<Autogen.Grpc.v1.InvokeBindingRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();
            int data = 10;
            (await FluentActions.Awaiting(async () => await client.DaprClient.InvokeBindingAsync<int>("test", "testOp", data, cancellationToken: ct))
                .Should().ThrowAsync<OperationCanceledException>())
                .WithInnerException<Grpc.Core.RpcException>();
        }

        private class InvokeRequest
        {
            public string RequestParameter { get; set; }
        }
    }
}
