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
    using Grpc.Net.Client;
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

        private class InvokeRequest
        {
            public string RequestParameter { get; set; }
        }
    }
}
