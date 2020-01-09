// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    public class StateHttpClientTest
    {
        [Fact]
        public async Task GetStateAsync_CanReadState()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetStateAsync<Widget>("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetStateUrl(3500, "test"));

            entry.RespondWithJson(new Widget() { Size = "small", Color = "yellow", });

            var state = await task;
            state.Size.Should().Be("small");
            state.Color.Should().Be("yellow");
        }

        [Fact]
        public async Task GetStateAsync_CanReadEmptyState_ReturnsDefault()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetStateAsync<Widget>("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetStateUrl(3500, "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.OK));

            var state = await task;
            state.Should().BeNull();
        }

        [Fact]
        public async Task GetStateAsync_ThrowsForNonSuccess()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetStateAsync<Widget>("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetStateUrl(3500, "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NotAcceptable));

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task GetStateAsync_WithBaseAddress_GeneratesCorrectUrl()
        {
            var httpClient = new TestHttpClient()
            {
                BaseAddress = new Uri("http://localhost:5000"),
            };
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetStateAsync<Widget>("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetStateUrl(5000, "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.OK));

            await task;
        }

        [Fact]
        public async Task SaveStateAsync_CanSaveState()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var widget = new Widget() { Size = "small", Color = "yellow", };
            var task = client.SaveStateAsync("test", widget);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(SaveStateUrl(3500));

            using (var stream = await entry.Request.Content.ReadAsStreamAsync())
            {
                var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
                json.ValueKind.Should().Be(JsonValueKind.Array);
                json.GetArrayLength().Should().Be(1);
                json[0].GetProperty("key").GetString().Should().Be("test");

                var value = JsonSerializer.Deserialize<Widget>(json[0].GetProperty("value").GetRawText());
                value.Size.Should().Be("small");
                value.Color.Should().Be("yellow");
            }

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NoContent));
            await task;
        }

        [Fact]
        public async Task SaveStateAsync_CanClearState()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.SaveStateAsync<object>("test", null);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(SaveStateUrl(3500));

            using (var stream = await entry.Request.Content.ReadAsStreamAsync())
            {
                var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
                json.ValueKind.Should().Be(JsonValueKind.Array);
                json.GetArrayLength().Should().Be(1);
                json[0].GetProperty("key").GetString().Should().Be("test");

                json[0].GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
            }

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NoContent));
            await task;
        }

        [Fact]
        public async Task SetStateAsync_ThrowsForNonSuccess()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var widget = new Widget() { Size = "small", Color = "yellow", };
            var task = client.SaveStateAsync("test", widget);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(SaveStateUrl(3500));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NotAcceptable));

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task SetStateAsync_WithBaseAddress_GeneratesCorrectUrl()
        {
            var httpClient = new TestHttpClient()
            {
                BaseAddress = new Uri("http://localhost:5000/"),
            };
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var widget = new Widget() { Size = "small", Color = "yellow", };
            var task = client.SaveStateAsync("test", widget);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(SaveStateUrl(5000));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.OK));

            await task;
        }

        [Fact]
        public async Task DeleteStateAsync_CanDeleteState()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.DeleteStateAsync("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(DeleteStateUrl(3500, "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.OK));
            await task;
        }

        [Fact]
        public async Task DeleteStateAsync_ThrowsForNonSuccess()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.DeleteStateAsync("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(DeleteStateUrl(3500, "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NotAcceptable));

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task GetStateEntryAsync_CanReadState()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetStateEntryAsync<Widget>("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetStateUrl(3500, "test"));

            entry.RespondWithJson(new Widget() { Size = "small", Color = "yellow", });

            var state = await task;
            state.Key.Should().Be("test");
            state.Value.Size.Should().Be("small");
            state.Value.Color.Should().Be("yellow");
        }

        [Fact]
        public async Task GetStateEntryAsync_CanReadEmptyState_ReturnsDefault()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetStateEntryAsync<Widget>("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetStateUrl(3500, "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.OK));

            var state = await task;
            state.Key.Should().Be("test");
            state.Value.Should().BeNull();
        }

        [Fact]
        public async Task GetStateEntryAsync_CanSaveState()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetStateEntryAsync<Widget>("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetStateUrl(3500, "test"));

            entry.RespondWithJson(new Widget() { Size = "small", Color = "yellow", });

            var state = await task;
            state.Key.Should().Be("test");
            state.Value.Size.Should().Be("small");
            state.Value.Color.Should().Be("yellow");

            state.Value.Color = "green";
            var task2 = state.SaveAsync();

            httpClient.Requests.TryDequeue(out entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(SaveStateUrl(3500));

            using (var stream = await entry.Request.Content.ReadAsStreamAsync())
            {
                var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
                json.ValueKind.Should().Be(JsonValueKind.Array);
                json.GetArrayLength().Should().Be(1);
                json[0].GetProperty("key").GetString().Should().Be("test");

                var value = JsonSerializer.Deserialize<Widget>(json[0].GetProperty("value").GetRawText());
                value.Size.Should().Be("small");
                value.Color.Should().Be("green");
            }

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NoContent));
            await task;
        }

        [Fact]
        public async Task GetStateEntryAsync_CanDeleteState()
        {
            var httpClient = new TestHttpClient();
            var client = new StateHttpClient(httpClient, new JsonSerializerOptions());

            var task = client.GetStateEntryAsync<Widget>("test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetStateUrl(3500, "test"));

            entry.RespondWithJson(new Widget() { Size = "small", Color = "yellow", });

            var state = await task;
            state.Key.Should().Be("test");
            state.Value.Size.Should().Be("small");
            state.Value.Color.Should().Be("yellow");

            state.Value.Color = "green";
            var task2 = state.DeleteAsync();

            httpClient.Requests.TryDequeue(out entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(DeleteStateUrl(3500, "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.OK));
            await task;
        }

        private static string GetStateUrl(int port, string key)
        {
            return $"http://localhost:{port}/v1.0/state/{key}";
        }

        private static string SaveStateUrl(int port)
        {
            return $"http://localhost:{port}/v1.0/state";
        }

        private static string DeleteStateUrl(int port, string key)
        {
            return $"http://localhost:{port}/v1.0/state/{key}";
        }

        private class Widget
        {
            public string Size { get; set; }

            public string Color { get; set; }
        }
    }
}
