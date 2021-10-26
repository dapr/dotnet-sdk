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
                    json.GetArrayLength().Should().Be(12);
                    var subscriptions = new List<(string PubsubName, string Topic, string Route, string rawPayload, string match)>();
                    foreach (var element in json.EnumerateArray())
                    {
                        var pubsubName = element.GetProperty("pubsubName").GetString();
                        var topic = element.GetProperty("topic").GetString();
                        var rawPayload = string.Empty;
                        if (element.TryGetProperty("metadata", out JsonElement metadata))
                        {
                            rawPayload = metadata.GetProperty("rawPayload").GetString();
                        }

                        if (element.TryGetProperty("route", out JsonElement route))
                        {
                            subscriptions.Add((pubsubName, topic, route.GetString(), rawPayload, string.Empty));
                        }
                        else if (element.TryGetProperty("routes", out JsonElement routes))
                        {
                            if (routes.TryGetProperty("rules", out JsonElement rules))
                            {
                                foreach (var rule in rules.EnumerateArray())
                                {
                                    var match = rule.GetProperty("match").GetString();
                                    var path = rule.GetProperty("path").GetString();
                                    subscriptions.Add((pubsubName, topic, path, rawPayload, match));
                                }
                            }
                            if (routes.TryGetProperty("default", out JsonElement defaultProperty))
                            {
                                subscriptions.Add((pubsubName, topic, defaultProperty.GetString(), rawPayload, string.Empty));
                            }                            
                        }
                    }

                    subscriptions.Should().Contain(("testpubsub", "A", "topic-a", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("testpubsub", "A.1", "topic-a", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("pubsub", "B", "B", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("custom-pubsub", "custom-C", "C", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("pubsub", "register-user", "register-user", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("pubsub", "register-user-plaintext", "register-user-plaintext", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("pubsub", "D", "D", "true", string.Empty));
                    subscriptions.Should().Contain(("pubsub", "E", "E", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("pubsub", "E", "E-Critical", string.Empty, "event.type == \"critical\""));
                    subscriptions.Should().Contain(("pubsub", "E", "E-Important", string.Empty, "event.type == \"important\""));
                    subscriptions.Should().Contain(("pubsub", "F", "multiTopicAttr", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("pubsub", "F.1", "multiTopicAttr", "true", string.Empty));
                    subscriptions.Should().Contain(("pubsub", "splitTopicBuilder", "splitTopics", string.Empty, string.Empty));
                    subscriptions.Should().Contain(("pubsub", "splitTopicAttr", "splitTopics", "true", string.Empty));

                    // Test priority route sorting
                    var eTopic = subscriptions.FindAll(e => e.Topic == "E");
                    eTopic.Count.Should().Be(3);
                    eTopic[0].Route.Should().Be("E-Critical");
                    eTopic[1].Route.Should().Be("E-Important");
                    eTopic[2].Route.Should().Be("E");
                }
            }
        }
    }
}
