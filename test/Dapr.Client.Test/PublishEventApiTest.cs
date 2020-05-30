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
        public async Task PublishEventAsync_CanPublishTopicWithData()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions{ HttpClient = httpClient })
                .Build();
          
            var publishData = new PublishData() { PublishObjectParameter = "testparam" };
            var task = daprClient.PublishEventAsync<PublishData>("test", publishData);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<PublishEventRequest>(entry.Request);
            var jsonFromRequest = request.Data.ToStringUtf8();

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


            var task = daprClient.PublishEventAsync("test");
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<PublishEventRequest>(entry.Request);
            var jsonFromRequest = request.Data.ToStringUtf8();

            request.Topic.Should().Be("test");
            jsonFromRequest.Should().Be("\"\"");
        }
        
        private class PublishData
        {
            public string PublishObjectParameter { get; set; }
        }
    }
}
