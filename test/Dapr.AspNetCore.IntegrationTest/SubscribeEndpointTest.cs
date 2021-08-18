// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
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
                    json.GetArrayLength().Should().Be(7);
                    var subscriptions = new List<(string PubsubName, string Topic, string Route, string rawPayload)>();
                    foreach (var element in json.EnumerateArray())
                    {
                        var pubsubName = element.GetProperty("pubsubName").GetString();
                        var topic = element.GetProperty("topic").GetString();
                        var route = element.GetProperty("route").GetString();
                        var rawPayload = string.Empty;
                        if(element.TryGetProperty("metadata", out JsonElement metadata))
                        {
                            rawPayload = metadata.GetProperty("rawPayload").GetString();
                        }
                        
                        subscriptions.Add((pubsubName, topic, route,rawPayload));
                    }

                    subscriptions.Should().Contain(("testpubsub", "A", "topic-a", string.Empty));
                    subscriptions.Should().Contain(("pubsub", "B", "B", string.Empty));
                    subscriptions.Should().Contain(("custom-pubsub", "custom-C", "C", string.Empty));
                    subscriptions.Should().Contain(("pubsub", "register-user", "register-user", string.Empty));
                    subscriptions.Should().Contain(("pubsub", "register-user-plaintext", "register-user-plaintext", string.Empty));
                    subscriptions.Should().Contain(("pubsub", "D", "D", "true"));
                    subscriptions.Should().Contain(("pubsub", "E", "E", "false"));
                }
            }
        }
    }
}
