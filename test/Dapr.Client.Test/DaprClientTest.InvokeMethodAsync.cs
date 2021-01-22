// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.AppCallback.Autogen.Grpc.v1;
    using Dapr.Client;
    using Dapr.Client.Autogen.Grpc.v1;
    using Dapr.Client.Autogen.Test.Grpc.v1;
    using FluentAssertions;
    using Grpc.Core;
    using Moq;
    using Xunit;

    // Most of the InvokeMethodAsync functionality on DaprClient is non-abstract methods that
    // forward to a few different entry points to create a message, or send a message and process
    // its result.
    //
    // So we write basic tests for all of those that every parameter passing is correct, and then
    // test the specialized methods in detail.
    public partial class DaprClientTest
    {
        private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            // Use case sensitive settings for tests, this way we verify that the same settings are being
            // used in all calls.
            PropertyNameCaseInsensitive  = false,
        };

        [Fact]
        public async Task InvokeMethodAsync_VoidVoidNoHttpMethod_Success()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var task = client.InvokeMethodAsync("app1", "mymethod");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            Assert.Equal(entry.Request.Method, HttpMethod.Post);
            Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, entry.Request.RequestUri.AbsoluteUri);
            Assert.Null(entry.Request.Content);

            entry.Respond(new HttpResponseMessage());
            await task;
        }

        [Fact]
        public async Task InvokeMethodAsync_VoidVoidWithHttpMethod_Success()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var task = client.InvokeMethodAsync(HttpMethod.Put, "app1", "mymethod");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            Assert.Equal(entry.Request.Method, HttpMethod.Put);
            Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, entry.Request.RequestUri.AbsoluteUri);
            Assert.Null(entry.Request.Content);

            entry.Respond(new HttpResponseMessage());
            await task;
        }

        [Fact]
        public async Task InvokeMethodAsync_VoidResponseNoHttpMethod_Success()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var task = client.InvokeMethodAsync<Widget>("app1", "mymethod");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            Assert.Equal(entry.Request.Method, HttpMethod.Post);
            Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, entry.Request.RequestUri.AbsoluteUri);
            Assert.Null(entry.Request.Content);

            var expected = new Widget()
            {
                Color = "red",
            };

            entry.RespondWithJson(expected, jsonSerializerOptions);
            var actual = await task;
            Assert.Equal(expected.Color, actual.Color);
        }

        [Fact]
        public async Task InvokeMethodAsync_VoidResponseWithHttpMethod_Success()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var task = client.InvokeMethodAsync<Widget>(HttpMethod.Put, "app1", "mymethod");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            Assert.Equal(entry.Request.Method, HttpMethod.Put);
            Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, entry.Request.RequestUri.AbsoluteUri);
            Assert.Null(entry.Request.Content);

            var expected = new Widget()
            {
                Color = "red",
            };

            entry.RespondWithJson(expected, jsonSerializerOptions);
            var actual = await task;
            Assert.Equal(expected.Color, actual.Color);
        }

        [Fact]
        public async Task InvokeMethodAsync_RequestVoidNoHttpMethod_Success()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var data = new Widget()
            {
                Color = "red",
            };

            var task = client.InvokeMethodAsync<Widget>("app1", "mymethod", data);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            Assert.Equal(entry.Request.Method, HttpMethod.Post);
            Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, entry.Request.RequestUri.AbsoluteUri);

            var content = Assert.IsType<JsonContent>(entry.Request.Content);
            Assert.Equal(data.GetType(), content.ObjectType);
            Assert.Same(data, content.Value);

            entry.Respond(new HttpResponseMessage());
            await task;
        }

        [Fact]
        public async Task InvokeMethodAsync_RequestVoidWithHttpMethod_Success()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var data = new Widget()
            {
                Color = "red",
            };

            var task = client.InvokeMethodAsync<Widget>(HttpMethod.Put, "app1", "mymethod", data);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            Assert.Equal(entry.Request.Method, HttpMethod.Put);
            Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, entry.Request.RequestUri.AbsoluteUri);
            
            var content = Assert.IsType<JsonContent>(entry.Request.Content);
            Assert.Equal(data.GetType(), content.ObjectType);
            Assert.Same(data, content.Value);

            entry.Respond(new HttpResponseMessage());
            await task;
        }

        [Fact]
        public async Task InvokeMethodAsync_RequestResponseNoHttpMethod_Success()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var data = new Widget()
            {
                Color = "red",
            };

            var task = client.InvokeMethodAsync<Widget, Widget>("app1", "mymethod", data);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            Assert.Equal(entry.Request.Method, HttpMethod.Post);
            Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, entry.Request.RequestUri.AbsoluteUri);

            var content = Assert.IsType<JsonContent>(entry.Request.Content);
            Assert.Equal(data.GetType(), content.ObjectType);
            Assert.Same(data, content.Value);

            entry.RespondWithJson(data, jsonSerializerOptions);
            var actual = await task;
            Assert.Equal(data.Color, actual.Color);
        }

        [Fact]
        public async Task InvokeMethodAsync_RequestResponseWithHttpMethod_Success()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var data = new Widget()
            {
                Color = "red",
            };

            var task = client.InvokeMethodAsync<Widget, Widget>(HttpMethod.Put, "app1", "mymethod", data);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            Assert.Equal(entry.Request.Method, HttpMethod.Put);
            Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, entry.Request.RequestUri.AbsoluteUri);
            
            var content = Assert.IsType<JsonContent>(entry.Request.Content);
            Assert.Equal(data.GetType(), content.ObjectType);
            Assert.Same(data, content.Value);

            entry.RespondWithJson(data, jsonSerializerOptions);
            var actual = await task;
            Assert.Equal(data.Color, actual.Color);
        }

        [Theory]
        [InlineData("", "https://test-endpoint:3501/v1.0/invoke/test-app/method/")]
        [InlineData("/", "https://test-endpoint:3501/v1.0/invoke/test-app/method/")]
        [InlineData("mymethod", "https://test-endpoint:3501/v1.0/invoke/test-app/method/mymethod")]
        [InlineData("/mymethod", "https://test-endpoint:3501/v1.0/invoke/test-app/method/mymethod")]
        [InlineData("mymethod?key1=value1&key2=value2#fragment", "https://test-endpoint:3501/v1.0/invoke/test-app/method/mymethod?key1=value1&key2=value2#fragment")]

        // garbage in -> garbage out - we don't deeply inspect what you pass.
        [InlineData("http://example.com", "https://test-endpoint:3501/v1.0/invoke/test-app/method/http://example.com")]
        public void CreateInvokeMethodRequest_TransformsUrlCorrectly(string method, string expected)
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var request = client.CreateInvokeMethodRequest("test-app", method);
            Assert.Equal(new Uri(expected).AbsoluteUri, request.RequestUri.AbsoluteUri);
        }

        [Fact]
        public async Task CreateInvokeMethodRequest_WithData_CreatesJsonContent()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var data = new Widget
            {
                Color = "red",
            };

            var request = client.CreateInvokeMethodRequest("test-app", "test", data);
            var content = Assert.IsType<JsonContent>(request.Content);
            Assert.Equal(typeof(Widget), content.ObjectType);
            Assert.Same(data, content.Value);

            // the best way to verify the usage of the correct settings object
            var actual = await content.ReadFromJsonAsync<Widget>(this.jsonSerializerOptions);
            Assert.Equal(data.Color, actual.Color);
        }

        [Fact]
        public async Task InvokeMethodWithResponseAsync_ReturnsMessageWithoutCheckingStatus()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var request = client.CreateInvokeMethodRequest("test-app", "test");
            var task = client.InvokeMethodWithResponseAsync(request);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Respond(new HttpResponseMessage(HttpStatusCode.BadRequest)); // Non-2xx response

            var response = await task;
        }

        [Fact]
        public async Task InvokeMethodWithResponseAsync_PreventsNonDaprRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var client = new DaprClientGrpc(
                Mock.Of<global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient>(), 
                httpClient,
                new Uri("https://test-endpoint:3501"),
                jsonSerializerOptions);

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            {
                await client.InvokeMethodWithResponseAsync(request);
            });

            Assert.Equal("The provided request URI is not a Dapr service invocation URI.", ex.Message);
        }

        private class Widget
        {
            public string Color { get; set; }
        }
    }
}
