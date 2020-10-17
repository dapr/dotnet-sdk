// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc.v1;
    using Dapr.Client.Autogen.Test.Grpc.v1;
    using Dapr.AppCallback.Autogen.Grpc.v1;
    using Dapr.Client.Http;
    using FluentAssertions;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Xunit;
    using System.Linq;

    public class InvokeApiTest
    {
        [Fact]
        public async Task InvokeMethodAsync_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var queryString = new Dictionary<string, string>();
            queryString.Add("key1", "value1");
            queryString.Add("key2", "value2");

            var httpExtension = new Http.HTTPExtension()
            {
                Verb = HTTPVerb.Post,
                QueryString = queryString
            };

            var task = daprClient.InvokeMethodAsync<Response>("app1", "mymethod", httpExtension);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);

            envelope.Id.Should().Be("app1");
            envelope.Message.Method.Should().Be("mymethod");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);
            envelope.Message.HttpExtension.Verb.Should().Be(Autogen.Grpc.v1.HTTPExtension.Types.Verb.Post);
            envelope.Message.HttpExtension.Querystring.Count.Should().Be(2);
            envelope.Message.HttpExtension.Querystring.ContainsKey("key1").Should().BeTrue();
            envelope.Message.HttpExtension.Querystring.ContainsKey("key2").Should().BeTrue();
            envelope.Message.HttpExtension.Querystring["key1"].Should().Be("value1");
            envelope.Message.HttpExtension.Querystring["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task InvokeMethodAsync_NoVerbSpecifiedByUser_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            // httpExtension not specified
            var task = daprClient.InvokeMethodAsync<Response>("app1", "mymethod");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);

            envelope.Id.Should().Be("app1");
            envelope.Message.Method.Should().Be("mymethod");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            envelope.Message.HttpExtension.Verb.Should().Be(Autogen.Grpc.v1.HTTPExtension.Types.Verb.Post);
            envelope.Message.HttpExtension.Querystring.Count.Should().Be(0);
        }

        [Fact]
        public void InvokeMethodAsync_HeadersSpecifiedByUser_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var headers = new Dictionary<string, string>();
            headers.Add("Authorization", "Bearer foo");
            headers.Add("X-Custom", "bar");

            var httpExtension = new Http.HTTPExtension()
            {
                Verb = HTTPVerb.Post,
                Headers = headers
            };

            var task = daprClient.InvokeMethodAsync<Response>("app1", "mymethod", httpExtension);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();

            entry.Request.Headers.Authorization.Scheme.Should().Be("Bearer");
            entry.Request.Headers.Authorization.Parameter.Should().Be("foo");
            entry.Request.Headers.GetValues("X-Custom").FirstOrDefault().Should().Be("bar");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var clientBuilder = new DaprClientBuilder();
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Response>("test", "test");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            SendResponse(clientBuilder.GrpcSerializer, data, entry);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData_EmptyResponseReturnsNull()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var clientBuilder = new DaprClientBuilder();
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " });

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<Request>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            // Create Response & Respond
            SendResponse<Response>(clientBuilder.GrpcSerializer, null, entry);

            // Validate Response.
            var invokedResponse = await task;
            invokedResponse.Should().BeNull();
        }


        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData_ThrowsExceptionForNonSuccess()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test", new Request() { RequestParameter = "Hello " });

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<Request>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            // Create Response & Respond
            var response = GrpcUtils.CreateResponse(HttpStatusCode.NotAcceptable);
            entry.Completion.SetResult(response);

            //validate response
            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeNoData()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var clientBuilder = new DaprClientBuilder();
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Response>("test", "test");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            // Create Response & Respond
            var data = new Response() { Name = "Look, I was invoked!" };
            SendResponse(clientBuilder.GrpcSerializer, data, entry);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeNoData_ThrowsExceptionNonSuccess()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Response>("test", "test");

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            // Create Response & Respond
            var response = GrpcUtils.CreateResponse(HttpStatusCode.NotAcceptable);
            entry.Completion.SetResult(response);

            //validate response
            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithNoReturnTypeAndData()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var clientBuilder = new DaprClientBuilder();
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            Request request = new Request() { RequestParameter = "Hello " };
            var task = daprClient.InvokeMethodAsync<Request>("test", "test", request);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<Request>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            // Create Response & Respond
            var response = new Response() { Name = "Look, I was invoked!" };
            SendResponse(clientBuilder.GrpcSerializer, response, entry);

            FluentActions.Awaiting(async () => await task).Should().NotThrow();
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithNoReturnTypeAndData_ThrowsErrorNonSuccess()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<Request>("test", "test", new Request() { RequestParameter = "Hello " });

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<Request>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            // Create Response & Respond
            var response = GrpcUtils.CreateResponse(HttpStatusCode.NotAcceptable);
            entry.Completion.SetResult(response);

            //validate response
            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task InvokeMethodAsync_WithNoReturnTypeAndData_UsesConfiguredJsonSerializerOptions()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var daprClient = new DaprClientBuilder(new GrpcSerializer(jsonOptions))
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var invokeRequest = new Request() { RequestParameter = "Hello" };
            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test", invokeRequest);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));
        }

        [Fact]
        public async Task InvokeMethodAsync_WithReturnTypeAndData_UsesConfiguredJsonSerializerOptions()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var clientBuilder = new DaprClientBuilder(new GrpcSerializer(jsonOptions));
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var invokeRequest = new Request() { RequestParameter = "Hello " };
            var invokedResponse = new Response { Name = "Look, I was invoked!" };

            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test", invokeRequest);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));

            SendResponse(clientBuilder.GrpcSerializer, invokedResponse, entry);
            var response = await task;

            response.Name.Should().Be(invokedResponse.Name);
        }

        [Fact]
        public async Task InvokeMethodAsync_WithReturnTypeAndData_WithNonDefaultVerb_WithQueryString_UsesConfiguredJsonSerializerOptions()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var clientBuilder = new DaprClientBuilder(new GrpcSerializer(jsonOptions));
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var invokeRequest = new Request() { RequestParameter = "Hello " };
            var invokedResponse = new Response { Name = "Look, I was invoked!" };

            Dictionary<string, string> queryString = new Dictionary<string, string>();
            queryString.Add("key1", "value1");
            var httpExtension = new Http.HTTPExtension()
            {
                Verb = HTTPVerb.Put,
                QueryString = queryString
            };

            var task = daprClient.InvokeMethodAsync<Request, Response>("test", "test1", invokeRequest, httpExtension);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test1");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);
            envelope.Message.HttpExtension.Verb.Should().Be(Autogen.Grpc.v1.HTTPExtension.Types.Verb.Put);
            envelope.Message.HttpExtension.Querystring.Count.Should().Be(1);
            envelope.Message.HttpExtension.Querystring.ContainsKey("key1").Should().BeTrue();
            envelope.Message.HttpExtension.Querystring["key1"].Should().Be("value1");


            var json = envelope.Message.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));

            SendResponse(clientBuilder.GrpcSerializer, invokedResponse, entry);
            var response = await task;

            response.Name.Should().Be(invokedResponse.Name);
        }

        [Fact]
        public async Task InvokeMethodAsync_AppCallback_SayHello()
        {
            // Configure Client
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var clientBuilder = new DaprClientBuilder(new GrpcSerializer(jsonOptions));
            var httpClient = new AppCallbackClient(new DaprAppCallbackService(clientBuilder.GrpcSerializer));
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var request = new Request() { RequestParameter = "Look, I was invoked!" };

            var response = await daprClient.InvokeMethodAsync<Request, Response>("test", "SayHello", request);

            response.Name.Should().Be("Hello Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_AppCallback_RepeatedField()
        {
            // Configure Client
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var clientBuilder = new DaprClientBuilder(new GrpcSerializer(jsonOptions));
            var httpClient = new AppCallbackClient(new DaprAppCallbackService(clientBuilder.GrpcSerializer));
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var testRun = new TestRun();
            testRun.Tests.Add(new TestCase() { Name = "test1" });
            testRun.Tests.Add(new TestCase() { Name = "test2" });
            testRun.Tests.Add(new TestCase() { Name = "test3" });

            var response = await daprClient.InvokeMethodAsync<TestRun, TestRun>("test", "TestRun", testRun);

            response.Tests.Count.Should().Be(3);
            response.Tests[0].Name.Should().Be("test1");
            response.Tests[1].Name.Should().Be("test2");
            response.Tests[2].Name.Should().Be("test3");
        }

        [Fact]
        public async Task InvokeMethodAsync_AppCallback_UnexpectedMethod()
        {
            // Configure Client
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var clientBuilder = new DaprClientBuilder(new GrpcSerializer(jsonOptions));
            var httpClient = new AppCallbackClient(new DaprAppCallbackService(clientBuilder.GrpcSerializer));
            var daprClient = clientBuilder
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var request = new Request() { RequestParameter = "Look, I was invoked!" };

            var response = await daprClient.InvokeMethodAsync<Request, Response>("test", "not-existing", request);

            response.Name.Should().Be("unexpected");
        }

        private async void SendResponse<T>(GrpcSerializer serializer, T data, TestHttpClient.Entry entry)
        {
            var dataAny = serializer.ToAny(data);
            var dataResponse = new InvokeResponse();
            dataResponse.Data = dataAny;

            var streamContent = await GrpcUtils.CreateResponseContent(dataResponse);
            var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            entry.Completion.SetResult(response);
        }

        private class Request
        {
            public string RequestParameter { get; set; }
        }

        private class Response
        {
            public string Name { get; set; }
        }

        // Test implementation of the AppCallback.AppCallbackBase service
        private class DaprAppCallbackService : AppCallback.AppCallbackBase
        {
            private readonly GrpcSerializer serializer;

            public DaprAppCallbackService(GrpcSerializer serializer)
            {
                this.serializer = serializer;
            }

            public override Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
            {
                return request.Method switch
                {
                    "SayHello" => SayHello(request),
                    "TestRun" => TestRun(request),
                    _ => Task.FromResult(new InvokeResponse
                    {
                        Data = serializer.ToAny(new Response { Name = $"unexpected" })
                    })
                };
            }

            private Task<InvokeResponse> SayHello(InvokeRequest request)
            {
                var helloRequest = serializer.FromAny<Request>(request.Data);
                var helloResponse = new Response { Name = $"Hello {helloRequest.RequestParameter}" };

                return Task.FromResult(new InvokeResponse
                {
                    Data = serializer.ToAny(helloResponse)
                });
            }

            private Task<InvokeResponse> TestRun(InvokeRequest request)
            {
                var echoRequest = serializer.FromAny<TestRun>(request.Data);

                return Task.FromResult(new InvokeResponse
                {
                    Data = serializer.ToAny(echoRequest)
                });
            }
        }
    }
}
