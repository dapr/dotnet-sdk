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
    using Dapr.Client.Http;
    using FluentAssertions;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Xunit;

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

            var task = daprClient.InvokeMethodAsync<InvokedResponse>("app1", "mymethod", httpExtension);

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
            var task = daprClient.InvokeMethodAsync<InvokedResponse>("app1", "mymethod");

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
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<InvokedResponse>("test", "test");

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            // Create Response & Respond
            var data = new InvokedResponse() { Name = "Look, I was invoked!" };
            SendResponse(data, entry);

            // Validate Response
            var invokedResponse = await task;
            invokedResponse.Name.Should().Be("Look, I was invoked!");
        }

        [Fact]
        public async Task InvokeMethodAsync_CanInvokeMethodWithReturnTypeAndData_EmptyResponseReturnsNull()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<InvokeRequest, InvokedResponse>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            // Create Response & Respond
            SendResponse<InvokedResponse>(null, entry);

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

            var task = daprClient.InvokeMethodAsync<InvokeRequest, InvokedResponse>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json);
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
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.InvokeMethodAsync<InvokedResponse>("test", "test");

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            // Create Response & Respond
            var data = new InvokedResponse() { Name = "Look, I was invoked!" };
            SendResponse(data, entry);

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

            var task = daprClient.InvokeMethodAsync<InvokedResponse>("test", "test");

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
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            InvokeRequest invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var task = daprClient.InvokeMethodAsync<InvokeRequest>("test", "test", invokeRequest);

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            // Create Response & Respond
            var response = new InvokedResponse() { Name = "Look, I was invoked!" };
            SendResponse(response, entry);

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

            var task = daprClient.InvokeMethodAsync<InvokeRequest>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json);
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
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello" };
            var task = daprClient.InvokeMethodAsync<InvokeRequest, InvokedResponse>("test", "test", invokeRequest);

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
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var invokedResponse = new InvokedResponse { Name = "Look, I was invoked!" };

            var task = daprClient.InvokeMethodAsync<InvokeRequest, InvokedResponse>("test", "test", invokeRequest);

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);

            var json = envelope.Message.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));

            SendResponse(invokedResponse, entry, jsonOptions);
            var response = await task;

            response.Name.Should().Be(invokedResponse.Name);
        }

        [Fact]
        public async Task InvokeMethodAsync_WithReturnTypeAndData_WithNonDefaultVerb_WithQueryString_UsesConfiguredJsonSerializerOptions()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            var invokeRequest = new InvokeRequest() { RequestParameter = "Hello " };
            var invokedResponse = new InvokedResponse { Name = "Look, I was invoked!" };

            Dictionary<string, string> queryString = new Dictionary<string, string>();
            queryString.Add("key1", "value1");
            var httpExtension = new Http.HTTPExtension()
            {
                Verb = HTTPVerb.Put,
                QueryString = queryString
            };

            var task = daprClient.InvokeMethodAsync<InvokeRequest, InvokedResponse>("test", "test", invokeRequest, httpExtension);

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Message.Method.Should().Be("test");
            envelope.Message.ContentType.Should().Be(Constants.ContentTypeApplicationJson);
            envelope.Message.HttpExtension.Verb.Should().Be(Autogen.Grpc.v1.HTTPExtension.Types.Verb.Put);
            envelope.Message.HttpExtension.Querystring.Count.Should().Be(1);
            envelope.Message.HttpExtension.Querystring.ContainsKey("key1").Should().BeTrue();
            envelope.Message.HttpExtension.Querystring["key1"].Should().Be("value1");


            var json = envelope.Message.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));

            SendResponse(invokedResponse, entry, jsonOptions);
            var response = await task;

            response.Name.Should().Be(invokedResponse.Name);
        }

        private async void SendResponse<T>(T data, TestHttpClient.Entry entry, JsonSerializerOptions options = null)
        {
            var dataAny = ProtobufUtils.ConvertToAnyAsync(data, options);
            var dataResponse = new InvokeResponse();
            dataResponse.Data = dataAny;

            var streamContent = await GrpcUtils.CreateResponseContent(dataResponse);
            var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            entry.Completion.SetResult(response);
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
