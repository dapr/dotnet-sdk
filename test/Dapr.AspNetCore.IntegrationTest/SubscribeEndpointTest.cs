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
                    var subscriptions = await JsonSerializer.DeserializeAsync<List<Subscription>>(stream, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });
                    subscriptions.Count.Should().Be(7);

                    subscriptions.Find(s => s.Topic == "A" && s.PubsubName == "testpubsub" && s.Route == "topic-a" && s.Metadata == null).Should().NotBeNull();
                    subscriptions.Find(s => s.Topic == "B" && s.PubsubName == "pubsub" && s.Route == "B" && s.Metadata == null).Should().NotBeNull();
                    subscriptions.Find(s => s.Topic == "custom-C" && s.PubsubName == "custom-pubsub" && s.Route == "C" && s.Metadata == null).Should().NotBeNull();
                    subscriptions.Find(s => s.Topic == "D" && s.PubsubName == "pubsub" && s.Route == "D" && s.Metadata.RawPayload == "true").Should().NotBeNull();
                    subscriptions.Find(s => s.Topic == "E" && s.PubsubName == "pubsub" && s.Route == "E" && s.Metadata.RawPayload == "false").Should().NotBeNull();
                    subscriptions.Find(s => s.Topic == "register-user" && s.PubsubName == "pubsub" && s.Route == "register-user" && s.Metadata == null).Should().NotBeNull();
                    subscriptions.Find(s => s.Topic == "register-user-plaintext" && s.PubsubName == "pubsub" && s.Route == "register-user-plaintext" && s.Metadata == null).Should().NotBeNull();
                   
                }
            }
        }
    }
}
