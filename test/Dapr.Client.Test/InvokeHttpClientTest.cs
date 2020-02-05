// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    public class InvokeHttpClientTest
    {
        private const string DaprDefaultEndpoint = "127.0.0.1";

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var task = invokeClient.InvokeMethodAsync<InvokedResponse>("test", "test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.RespondWithJson(new InvokedResponse() { Name = "Look, I was invoked!" });

            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData_EmptyResponseReturnsNull()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var task = invokeClient.InvokeMethodAsync<InvokeRequest, InvokedResponse>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.OK));

            var invokedResponse = await task;
            invokedResponse.Should().BeNull();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData_ThrowsExceptionForNonSuccess()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var task = invokeClient.InvokeMethodAsync<InvokeRequest, InvokedResponse>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NotAcceptable));

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeNoData()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var task = invokeClient.InvokeMethodAsync<InvokedResponse>("test", "test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.RespondWithJson(new InvokedResponse() { Name = "Look, I was invoked!" });

            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeNoData_ThrowsExceptionNonSuccess()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var task = invokeClient.InvokeMethodAsync<InvokedResponse>("test", "test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NotAcceptable));

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public Task InvokeMethodAsync_CanInvokeMethodWithNoReturnTypeAndData()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var task = invokeClient.InvokeMethodAsync<InvokeRequest>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.RespondWithJson(new InvokedResponse() { Name = "Look, I was invoked!" });

            FluentActions.Awaiting(async () => await task).Should().NotThrow();

            return Task.FromResult(string.Empty);
        }

        [Fact]
        public Task InvokeMethodAsync_CanInvokeMethodWithNoReturnTypeAndData_ThrowsErrorNonSuccess()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var task = invokeClient.InvokeMethodAsync<InvokeRequest>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NotAcceptable));

            FluentActions.Awaiting(async () => await task).Should().ThrowAsync<HttpRequestException>();

            return Task.FromResult(string.Empty);
        }

        private static string GetInvokeUrl(int port, string serviceName, string methodName)
        {
            return $"http://{DaprDefaultEndpoint}:{port}/v1.0/invoke/{serviceName}/method/{methodName}";
        }

        private class InvokeRequest
        {
            public string RequestParameter { get; set; }
        }

        private class InvokedResponse
        {
            public string Name { get; set; }
        }
    }
}