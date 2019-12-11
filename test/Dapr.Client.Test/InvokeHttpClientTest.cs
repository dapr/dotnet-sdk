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
        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethod()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var invokeEnvelope = new InvokeEnvelope("test", "test", "{\"prop1\", \"data\"");

            var task = invokeClient.InvokeMethodAsync<InvokedResponse>(invokeEnvelope);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.RespondWithJson(new InvokedResponse() { Name = "Look, I was invoked!" });

            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethod_EmptyResponseReturnsNull()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var invokeEnvelope = new InvokeEnvelope("test", "test", "{\"prop1\", \"data\"");

            var task = invokeClient.InvokeMethodAsync<InvokedResponse>(invokeEnvelope);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.OK));

            var invokedResponse = await task;
            invokedResponse.Should().BeNull();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethod_ThrowsExceptionForNonSuccess()
        {
            var httpClient = new TestHttpClient();
            var invokeClient = new InvokeHttpClient(httpClient, new JsonSerializerOptions());

            var invokeEnvelope = new InvokeEnvelope("test", "test", "{\"prop1\", \"data\"");

            var task = invokeClient.InvokeMethodAsync<InvokedResponse>(invokeEnvelope);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.RequestUri.ToString().Should().Be(GetInvokeUrl(3500, "test", "test"));

            entry.Respond(new HttpResponseMessage(HttpStatusCode.NotAcceptable));

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<HttpRequestException>();
        }

        private static string GetInvokeUrl(int port, string serviceName, string methodName)
        {
            return $"http://localhost:{port}/v1.0/invoke/{serviceName}/method/{methodName}";
        }

        private class InvokedResponse
        {
            public string Name { get; set; }
        }
    }
}