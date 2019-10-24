// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.AspNetCore.IntegrationTest.App;
    using FluentAssertions;
    using Xunit;

    public class CloudEventsIntegrationTest
    {
        private readonly JsonSerializerOptions options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        [Fact]
        public async Task CanSendEmptyStructuredCloudEvent()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/B");
                request.Content = new StringContent("{}", Encoding.UTF8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
        }

        [Fact]
        public async Task CanSendStructuredCloudEvent()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/register-user");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            data = new
                            {
                                name = "jimmy",
                            },
                        }),
                    Encoding.UTF8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var userInfo = await JsonSerializer.DeserializeAsync<UserInfo>(await response.Content.ReadAsStreamAsync(), this.options);
                userInfo.Name.Should().Be("jimmy");
            }
        }

        [Fact]
        public async Task CanSendStructuredCloudEvent_WithContentType()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/register-user");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            data = new
                            {
                                name = "jimmy",
                            },
                            datacontenttype = "text/json",
                        }),
                    Encoding.UTF8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var userInfo = await JsonSerializer.DeserializeAsync<UserInfo>(await response.Content.ReadAsStreamAsync(), this.options);
                userInfo.Name.Should().Be("jimmy");
            }
        }

        // Yeah, I know, binary isn't a great term for this, it's what the cloudevents spec uses.
        // Basically this is here to test that an endpoint can handle requests with and without
        // an envelope.
        [Fact]
        public async Task CanSendBinaryCloudEvent_WithContentType()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/register-user");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            name = "jimmy",
                        }),
                    Encoding.UTF8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var userInfo = await JsonSerializer.DeserializeAsync<UserInfo>(await response.Content.ReadAsStreamAsync(), this.options);
                userInfo.Name.Should().Be("jimmy");
            }
        }
    }
}
