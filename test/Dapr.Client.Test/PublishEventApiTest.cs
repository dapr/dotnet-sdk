// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc.v1;
    using FluentAssertions;
    using Google.Rpc;
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
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var publishData = new PublishData() { PublishObjectParameter = "testparam" };
            var task = daprClient.PublishEventAsync<PublishData>(TestPubsubName, "test", publishData);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<PublishEventRequest>(entry.Request);
            var jsonFromRequest = request.Data.ToStringUtf8();

            request.PubsubName.Should().Be(TestPubsubName);
            request.Topic.Should().Be("test");
            jsonFromRequest.Should().Be(JsonSerializer.Serialize(publishData));
        }

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithNoContent()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();


            var task = daprClient.PublishEventAsync(TestPubsubName, "test");
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<PublishEventRequest>(entry.Request);
            var jsonFromRequest = request.Data.ToStringUtf8();

            request.PubsubName.Should().Be(TestPubsubName);
            request.Topic.Should().Be("test");
            jsonFromRequest.Should().Be("\"\"");
        }

        [Fact]
        public async Task PublishEventAsync_WithCancelledToken()
        {
            var client = new MockClient();

            const string rpcExceptionMessage = "Call canceled by client";
            const StatusCode rpcStatusCode = StatusCode.Cancelled;
            const string rpcStatusDetail = "Call canceled";

            var rpcStatus = new Grpc.Core.Status(rpcStatusCode, rpcStatusDetail);
            var rpcException = new Grpc.Core.RpcException(rpcStatus, new Metadata(), rpcExceptionMessage);

            var response = client.Publish().Build();

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock.Setup(m => m.PublishEventAsync(It.IsAny<PublishEventRequest>(), It.IsAny<CallOptions>()))
                       .Throws(rpcException);
            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();
            var task = client.DaprClient.PublishEventAsync(TestPubsubName, "test", cancellationToken: ct);
            (await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<OperationCanceledException>()).WithInnerException<Grpc.Core.RpcException>();
        }

        private class PublishData
        {
            public string PublishObjectParameter { get; set; }
        }
    }
}
