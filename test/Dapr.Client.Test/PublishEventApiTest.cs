﻿// ------------------------------------------------------------
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

    public class PublishEventApiTest
    {
        const string TestPubsubName = "testpubsubname";

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithData()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions{ HttpClient = httpClient })
                .Build();

            var publishData = new PublishData() { PublishObjectParameter = "testparam" };
            var task = daprClient.PublishEventAsync<PublishData>(TestPubsubName, "test", publishData);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<PublishEventRequest>(entry.Request);
            var jsonFromRequest = request.Data.ToStringUtf8();

            request.PubsubName.Should().Be(TestPubsubName);
            request.Topic.Should().Be("test");
            jsonFromRequest.Should().Be(JsonSerializer.Serialize(publishData));
            request.Metadata.Count.Should().Be(0);
        }

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithData_WithMetadata()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions{ HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            var publishData = new PublishData() { PublishObjectParameter = "testparam" };
            var task = daprClient.PublishEventAsync<PublishData>(TestPubsubName, "test", publishData, metadata);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<PublishEventRequest>(entry.Request);
            var jsonFromRequest = request.Data.ToStringUtf8();

            request.PubsubName.Should().Be(TestPubsubName);
            request.Topic.Should().Be("test");
            jsonFromRequest.Should().Be(JsonSerializer.Serialize(publishData));

            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");
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

            request.Metadata.Count.Should().Be(0);
        }

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithNoContent_WithMetadata()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            var task = daprClient.PublishEventAsync(TestPubsubName, "test", metadata);
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<PublishEventRequest>(entry.Request);
            var jsonFromRequest = request.Data.ToStringUtf8();

            request.PubsubName.Should().Be(TestPubsubName);
            request.Topic.Should().Be("test");
            jsonFromRequest.Should().Be("\"\"");

            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task PublishEventAsync_WithCancelledToken()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();

            await FluentActions.Awaiting(async () => await daprClient.PublishEventAsync(TestPubsubName, "test", cancellationToken: ct))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        private class PublishData
        {
            public string PublishObjectParameter { get; set; }
        }
    }
}
