// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    public class SecretHttpClientTest
    {
        private const string DaprDefaultEndpoint = "127.0.0.1";

        [Fact]
        public async Task GetStateAsync_CanReadState()
        {
            var httpClient = new TestHttpClient();
            var client = new SecretHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetSecretAsync("testStore", "test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetSecretUrl(3500, "testStore", "test"));

            entry.RespondWithJson(new Dictionary<string, string>() { { "_default", "secret" } });

            var secrets = await task;
            secrets.Count.Should().Be(1);
            secrets.ContainsKey("_default").Should().BeTrue();
            secrets["_default"].Should().Be("secret");
        }

        [Fact]
        public async Task GetStateAsync_ThrowsForNonSuccess()
        {
            var httpClient = new TestHttpClient();
            var client = new SecretHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetSecretAsync("testStore", "test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetSecretUrl(3500, "testStore", "test"));


            entry.Respond(new HttpResponseMessage(HttpStatusCode.NotAcceptable));

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task GetStateAsync_WithBaseAddress_GeneratesCorrectUrl()
        {
            var httpClient = new TestHttpClient()
            {
                BaseAddress = new Uri($"http://{DaprDefaultEndpoint}:5000"),
            };
            var client = new SecretHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetSecretAsync("testStore", "test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetSecretUrl(5000, "testStore", "test"));

            entry.RespondWithJson(new Dictionary<string, string>() { { "_default", "secret" } });

            await task;
        }

        private static string GetSecretUrl(int port, string storeName, string key)
        {
            return $"http://{DaprDefaultEndpoint}:{port}/v1.0/secrets/{storeName}/{key}";
        }
    }
}
