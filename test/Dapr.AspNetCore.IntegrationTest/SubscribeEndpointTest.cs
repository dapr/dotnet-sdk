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
                    json.GetArrayLength().Should().Be(3);
                    var topics = new List<string>();
                    foreach (var element in json.EnumerateArray())
                    {
                        topics.Add(element.GetString());
                    }

                    topics.Should().Contain("A");
                    topics.Should().Contain("B");
                    topics.Should().Contain("register-user");
                }
            }
        }
    }
}
