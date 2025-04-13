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

namespace Dapr.AspNetCore.IntegrationTest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

public class SubscribeEndpointTest
{
    [Fact]
    public async Task SubscribeEndpoint_ReportsTopics()
    {
        using (var factory = new AppWebApplicationFactory())
        {
            var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/dapr/subscribe");
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);

                json.ValueKind.ShouldBe(JsonValueKind.Array);
                json.GetArrayLength().ShouldBe(18);

                var subscriptions = new List<(string PubsubName, string Topic, string Route, string rawPayload, 
                    string match, string metadata, string DeadLetterTopic, string bulkSubscribeMetadata)>();

                foreach (var element in json.EnumerateArray())
                {
                    var pubsubName = element.GetProperty("pubsubName").GetString();
                    var topic = element.GetProperty("topic").GetString();
                    var rawPayload = string.Empty;
                    var deadLetterTopic = string.Empty;
                    var bulkSubscribeMetadata = string.Empty;
                    //JsonElement bulkSubscribeMetadata;
                    Dictionary<string, string> originalMetadata = new Dictionary<string, string>();

                    if (element.TryGetProperty("bulkSubscribe", out var BulkSubscribeMetadata))
                    {
                        bulkSubscribeMetadata = BulkSubscribeMetadata.ToString();
                    }
                    if (element.TryGetProperty("deadLetterTopic", out JsonElement DeadLetterTopic))
                    {
                        deadLetterTopic = DeadLetterTopic.GetString();
                    }
                    if (element.TryGetProperty("metadata", out JsonElement metadata))
                    {
                        if (metadata.TryGetProperty("rawPayload", out JsonElement rawPayloadJson))
                        {
                            rawPayload = rawPayloadJson.GetString();
                        }

                        foreach (var originalMetadataProperty in metadata.EnumerateObject().OrderBy(c => c.Name))
                        {
                            if (!originalMetadataProperty.Name.Equals("rawPayload"))
                            {
                                originalMetadata.Add(originalMetadataProperty.Name, originalMetadataProperty.Value.GetString());
                            }
                        }
                    }
                    var originalMetadataString = string.Empty;
                    if (originalMetadata.Count > 0)
                    {
                        originalMetadataString = string.Join(";", originalMetadata.Select(c => $"{c.Key}={c.Value}"));
                    }

                    if (element.TryGetProperty("route", out JsonElement route))
                    {
                        subscriptions.Add((pubsubName, topic, route.GetString(), rawPayload, string.Empty, 
                            originalMetadataString, deadLetterTopic, bulkSubscribeMetadata));
                    }
                    else if (element.TryGetProperty("routes", out JsonElement routes))
                    {
                        if (routes.TryGetProperty("rules", out JsonElement rules))
                        {
                            foreach (var rule in rules.EnumerateArray())
                            {
                                var match = rule.GetProperty("match").GetString();
                                var path = rule.GetProperty("path").GetString();
                                subscriptions.Add((pubsubName, topic, path, rawPayload, match, 
                                    originalMetadataString, deadLetterTopic, bulkSubscribeMetadata));
                            }
                        }
                        if (routes.TryGetProperty("default", out JsonElement defaultProperty))
                        {
                            subscriptions.Add((pubsubName, topic, defaultProperty.GetString(), rawPayload, 
                                string.Empty, originalMetadataString, deadLetterTopic, bulkSubscribeMetadata));
                        }
                    }
                }

                subscriptions.ShouldContain(("testpubsub", "A", "topic-a", string.Empty, string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("testpubsub", "A.1", "topic-a", string.Empty, string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "B", "B", string.Empty, string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("custom-pubsub", "custom-C", "C", string.Empty, string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "register-user", "register-user", string.Empty, string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "register-user-plaintext", "register-user-plaintext", string.Empty, string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "D", "D", "true", string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "E", "E", string.Empty, string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "E", "E-Critical", string.Empty, "event.type == \"critical\"", string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "E", "E-Important", string.Empty, "event.type == \"important\"", string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "F", "multiTopicAttr", string.Empty, string.Empty, string.Empty, string.Empty, 
                    "{\"enabled\":true,\"maxMessagesCount\":100,\"maxAwaitDurationMs\":1000}"));
                subscriptions.ShouldContain(("pubsub", "F.1", "multiTopicAttr", "true", string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "G", "G", string.Empty, string.Empty, string.Empty, "deadLetterTopicName", 
                    "{\"enabled\":true,\"maxMessagesCount\":300,\"maxAwaitDurationMs\":1000}"));
                subscriptions.ShouldContain(("pubsub", "splitTopicBuilder", "splitTopics", string.Empty, string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "splitTopicAttr", "splitTopics", "true", string.Empty, string.Empty, string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "metadata", "multiMetadataTopicAttr", string.Empty, string.Empty, "n1=v1;n2=v2,v3", string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "metadata.1", "multiMetadataTopicAttr", "true", string.Empty, "n1=v1", string.Empty, 
                    "{\"enabled\":true,\"maxMessagesCount\":500,\"maxAwaitDurationMs\":2000}"));
                subscriptions.ShouldContain(("pubsub", "splitMetadataTopicBuilder", "splitMetadataTopics", string.Empty, string.Empty, "n1=v1;n2=v1", string.Empty, String.Empty));
                subscriptions.ShouldContain(("pubsub", "metadataseparatorbyemptytring", "topicmetadataseparatorattrbyemptytring", string.Empty, string.Empty, "n1=v1,", string.Empty, String.Empty));
                // Test priority route sorting
                var eTopic = subscriptions.FindAll(e => e.Topic == "E");
                eTopic.Count.ShouldBe(3);
                eTopic[0].Route.ShouldBe("E-Critical");
                eTopic[1].Route.ShouldBe("E-Important");
                eTopic[2].Route.ShouldBe("E");
            }
        }
    }
}