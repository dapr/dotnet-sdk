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

    public class PublishEventApiTest
    {
        const string TestPubsubName = "testpubsubname";

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithData()
        {
            await using var client = TestClient.CreateForDaprClient();

            var publishData = new PublishData() { PublishObjectParameter = "testparam" };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                await daprClient.PublishEventAsync<PublishData>(TestPubsubName, "test", publishData);
            });

            request.Dismiss();

            var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
            var jsonFromRequest = envelope.Data.ToStringUtf8();

            envelope.DataContentType.Should().Be("application/json");
            envelope.PubsubName.Should().Be(TestPubsubName);
            envelope.Topic.Should().Be("test");
            jsonFromRequest.Should().Be(JsonSerializer.Serialize(publishData, client.InnerClient.JsonSerializerOptions));
            envelope.Metadata.Count.Should().Be(0);
        }

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithData_WithMetadata()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            var publishData = new PublishData() { PublishObjectParameter = "testparam" };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                await daprClient.PublishEventAsync<PublishData>(TestPubsubName, "test", publishData, metadata);
            });

            request.Dismiss();

            var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
            var jsonFromRequest = envelope.Data.ToStringUtf8();

            envelope.DataContentType.Should().Be("application/json");
            envelope.PubsubName.Should().Be(TestPubsubName);
            envelope.Topic.Should().Be("test");
            jsonFromRequest.Should().Be(JsonSerializer.Serialize(publishData, client.InnerClient.JsonSerializerOptions));

            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithNoContent()
        {
            await using var client = TestClient.CreateForDaprClient();

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                await daprClient.PublishEventAsync(TestPubsubName, "test");
            });

            request.Dismiss();

            var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
            var jsonFromRequest = envelope.Data.ToStringUtf8();

            envelope.PubsubName.Should().Be(TestPubsubName);
            envelope.Topic.Should().Be("test");
            envelope.Data.Length.Should().Be(0);
            envelope.Metadata.Count.Should().Be(0);
        }

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithNoContent_WithMetadata()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                await daprClient.PublishEventAsync(TestPubsubName, "test", metadata);
            });

            request.Dismiss();

            var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
            envelope.PubsubName.Should().Be(TestPubsubName);
            envelope.Topic.Should().Be("test");
            envelope.Data.Length.Should().Be(0);

            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task PublishEventAsync_WithCancelledToken()
        {
            await using var client = TestClient.CreateForDaprClient();

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await client.InnerClient.PublishEventAsync(TestPubsubName, "test", cancellationToken: cts.Token);
            });
        }

        // All overloads call through a common path that does exception handling.
        [Fact]
        public async Task PublishEventAsync_WrapsRpcException()
        {
            var client = new MockClient();

            var rpcStatus = new Status(StatusCode.Internal, "not gonna work");
            var rpcException = new RpcException(rpcStatus, new Metadata(), "not gonna work");

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.PublishEventAsync(It.IsAny<Autogen.Grpc.v1.PublishEventRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<DaprException>(async () => 
            {
                await client.DaprClient.PublishEventAsync("test", "test");
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        private class PublishData
        {
            public string PublishObjectParameter { get; set; }
        }
    }
}
