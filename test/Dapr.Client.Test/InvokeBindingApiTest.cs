// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc;
    using Dapr.Client.Autogen.Grpc.v1;
    using FluentAssertions;
    using Grpc.Core;
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

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");
            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var task = daprClient.InvokeBindingAsync<InvokeRequest>("test", invokeRequest, metadata);

            // Get Request and validate                     
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeBindingEnvelope>(entry.Request);
            envelope.Name.Should().Be("test");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");
            var json = envelope.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");
        }

        private class InvokeRequest
        {
            public string RequestParameter { get; set; }
        }
    }
}
