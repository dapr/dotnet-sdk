// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.Net;
using System.Net.Http.Headers;
using Dapr.Metadata.Abstractions;
using Dapr.Metadata.Runtime;
using Microsoft.Extensions.Configuration;

namespace Dapr.Metadata.Test;

public sealed class DaprMetadataProviderTests
{
    [Fact]
    public async Task GetAsync_FetchesMetadataFromDaprRuntime()
    {
        var handler = new QueueMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{ "id": "orders", "runtimeVersion": "1.15.0" }""")
        });
        var provider = CreateProvider(handler);

        var metadata = await provider.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("orders", metadata.AppId);
        Assert.Equal("1.15.0", metadata.RuntimeVersion);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://127.0.0.1:3501/v1.0/metadata", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetAsync_CachesMetadata()
    {
        var handler = new QueueMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{ "id": "first" }""") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{ "id": "second" }""") });
        var provider = CreateProvider(handler);

        var first = await provider.GetAsync(TestContext.Current.CancellationToken);
        var second = await provider.GetAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal("first", second.AppId);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RefreshAsync_FetchesNewMetadataAndUpdatesCache()
    {
        var handler = new QueueMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{ "id": "first" }""") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{ "id": "second" }""") });
        var provider = CreateProvider(handler);

        var first = await provider.GetAsync(TestContext.Current.CancellationToken);
        var refreshed = await provider.RefreshAsync(TestContext.Current.CancellationToken);
        var cached = await provider.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("first", first.AppId);
        Assert.Equal("second", refreshed.AppId);
        Assert.Same(refreshed, cached);
        Assert.Collection(
            handler.Requests,
            request => Assert.Equal("http://127.0.0.1:3501/v1.0/metadata", request.RequestUri?.ToString()),
            request => Assert.Equal("http://127.0.0.1:3501/v1.0/metadata", request.RequestUri?.ToString()));
    }

    [Fact]
    public async Task GetAsync_ThrowsForUnsuccessfulResponse()
    {
        var handler = new QueueMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent("""{ "error": "failed" }""")
        });
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<HttpRequestException>(async () => await provider.GetAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_ThrowsForEmptyJsonResponse()
    {
        var handler = new QueueMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("null")
        });
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.GetAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Empty Datpr metadata response", exception.Message);
    }

    [Fact]
    public async Task GetAsync_UsesConfiguredApiTokenHeader()
    {
        var handler = new QueueMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{ "id": "orders" }""")
        });
        var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["DAPR_API_TOKEN"] = "token-value"
        });

        await provider.GetAsync(TestContext.Current.CancellationToken);

        Assert.True(handler.Requests[0].Headers.TryGetValues("dapr-api-token", out var values));
        Assert.Equal("token-value", Assert.Single(values));
    }

    private static DaprMetadataProvider CreateProvider(QueueMessageHandler handler, Dictionary<string, string?>? configurationValues = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues ?? new Dictionary<string, string?>
            {
                ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:3501"
            })
            .Build();

        return new DaprMetadataProvider(new StaticHttpClientFactory(new HttpClient(handler)), new DaprMetadataRefreshSignal(), configuration);
    }

    private static StringContent JsonContent(string json)
    {
        var content = new StringContent(json);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }
}
