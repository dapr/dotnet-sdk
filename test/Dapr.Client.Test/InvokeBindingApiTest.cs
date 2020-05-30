// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Collections.Generic;
    using System.Text.Json;
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

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");
            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var task = daprClient.InvokeBindingAsync<InvokeRequest>("test", invokeRequest, metadata);

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

        private class InvokeRequest
        {
            public string RequestParameter { get; set; }
        }
    }
}
