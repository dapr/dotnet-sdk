// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    public class PublishHttpClientTest
    {
        private const string DaprDefaultEndpoint = "127.0.0.1";

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithContent()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new PublishHttpClient(httpClient, new JsonSerializerOptions());
            var publishContent = new PublishContent() { PublishObjectParameter = "testparam" };

            var task = invokeClient.PublishEventAsync<PublishContent>("test", publishContent);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetPublishUrl(3500, "test"));
            (await entry.Request.Content.ReadAsStringAsync()).Should().Be(JsonSerializer.Serialize(publishContent));
        }

        [Fact]
        public async Task PublishEventAsync_CanPublishTopicWithNoContent()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new PublishHttpClient(httpClient, new JsonSerializerOptions());

            var task = invokeClient.PublishEventAsync("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetPublishUrl(3500, "test"));

            (await entry.Request.Content.ReadAsStringAsync()).Should().Be("\"\"");
        }

        private static string GetPublishUrl(int port, string topicName)
        {
            return $"http://{DaprDefaultEndpoint}:{port}/v1.0/publish/{topicName}";
        }

        private class PublishContent
        {
            public string PublishObjectParameter { get; set; }
        }
    }
}