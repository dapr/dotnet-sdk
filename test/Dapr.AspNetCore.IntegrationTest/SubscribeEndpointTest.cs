// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    public class SubscribeEndpointTest
    {
        [Fact]
        public async Task SubscribeEndpoint_ReportsTopics()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/dapr/subscribe");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);

                    json.ValueKind.Should().Be(JsonValueKind.Array);
                    json.GetArrayLength().Should().Be(5);
                    var metadata = new List<(string PubsubName, string Topic, string Route)>();
                    foreach (var element in json.EnumerateArray())
                    {
                        var pubsubName = element.GetProperty("pubsubName").GetString();
                        var topic = element.GetProperty("topic").GetString();
                        var route = element.GetProperty("route").GetString();
                        metadata.Add((pubsubName, topic, route));
                    }

                    metadata.Should().Contain(("testpubsub", "A", "topic-a"));
                    metadata.Should().Contain(("pubsub", "B", "B"));
                    metadata.Should().Contain(("custom-pubsub", "custom-C", "C"));
                    metadata.Should().Contain(("pubsub", "register-user", "register-user"));
                    metadata.Should().Contain(("pubsub", "register-user-plaintext", "register-user-plaintext"));
                }
            }
        }
    }
}
