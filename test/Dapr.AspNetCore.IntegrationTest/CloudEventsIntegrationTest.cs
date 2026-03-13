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

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using App;
using Shouldly;
using Xunit;

public class CloudEventsIntegrationTest
{
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task CanSendEmptyStructuredCloudEvent()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/B")
        {
            Content = new StringContent("{}", Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");

        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }


    [Fact]
    public async Task CanSendStructuredCloudEvent()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/register-user")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        data = new
                        {
                            name = "jimmy",
                        },
                    }),
                Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");

        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var userInfo = await JsonSerializer.DeserializeAsync<UserInfo>(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), this.options, TestContext.Current.CancellationToken);
        userInfo.Name.ShouldBe("jimmy");
    }

    [Fact]
    public async Task CanSendStructuredCloudEvent_WithContentType()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/register-user")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        data = new
                        {
                            name = "jimmy",
                        },
                        datacontenttype = "text/json",
                    }),
                Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");

        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var userInfo = await JsonSerializer.DeserializeAsync<UserInfo>(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), this.options, TestContext.Current.CancellationToken);
        userInfo.Name.ShouldBe("jimmy");
    }

    [Fact]
    public async Task CanSendStructuredCloudEvent_WithNonJsonContentType()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/register-user-plaintext")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        data = "jimmy \"the cool guy\" smith",
                        datacontenttype = "text/plain",
                    }),
                Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");

        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        user.ShouldBe("jimmy \"the cool guy\" smith");
    }

    // Yeah, I know, binary isn't a great term for this, it's what the cloudevents spec uses.
    // Basically this is here to test that an endpoint can handle requests with and without
    // an envelope.
    [Fact]
    public async Task CanSendBinaryCloudEvent_WithContentType()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
            
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/register-user")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        name = "jimmy",
                    }),
                Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
            
        var userInfo = await JsonSerializer.DeserializeAsync<UserInfo>(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), this.options, TestContext.Current.CancellationToken);
        userInfo.Name.ShouldBe("jimmy");
    }
}
