// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc.v1;
    using FluentAssertions;
    using Grpc.Net.Client;
    using Xunit;

    public class PublishEventApiTest
    {
        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithContent()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions{ HttpClient = httpClient })
                .Build();
          
            var publishContent = new PublishContent() { PublishObjectParameter = "testparam" };
            var task = daprClient.PublishEventAsync<PublishContent>("test", publishContent);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<PublishEventEnvelope>(entry.Request);
            var jsonFromRequest = envelope.Data.Value.ToStringUtf8();

            envelope.Topic.Should().Be("test");
            jsonFromRequest.Should().Be(JsonSerializer.Serialize(publishContent));
        }

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithNoContent()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();


            var task = daprClient.PublishEventAsync("test");
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<PublishEventEnvelope>(entry.Request);
            var jsonFromRequest = envelope.Data.Value.ToStringUtf8();

            envelope.Topic.Should().Be("test");
            jsonFromRequest.Should().Be("\"\"");
        }
        
        private class PublishContent
        {
            public string PublishObjectParameter { get; set; }
        }
    }
}
