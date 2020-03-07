// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc;
    using FluentAssertions;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Xunit;

    public class InvokeApiTest
    {
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
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");            

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
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");
            var json = envelope.Data.Value.ToStringUtf8();
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
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");
            var json = envelope.Data.Value.ToStringUtf8();
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
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");

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
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");

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

            var task = daprClient.InvokeMethodAsync<InvokeRequest>("test", "test", new InvokeRequest() { RequestParameter = "Hello " });

            // Get Request and validate            
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");
            var json = envelope.Data.Value.ToStringUtf8();
            var typeFromRequest = JsonSerializer.Deserialize<InvokeRequest>(json);
            typeFromRequest.RequestParameter.Should().Be("Hello ");

            // Create Response & Respond
            var data = new InvokedResponse() { Name = "Look, I was invoked!" };
            SendResponse(data, entry);

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
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");
            var json = envelope.Data.Value.ToStringUtf8();
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
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");
            var json = envelope.Data.Value.ToStringUtf8();

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
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<InvokeServiceEnvelope>(entry.Request);
            envelope.Id.Should().Be("test");
            envelope.Method.Should().Be("test");
            var json = envelope.Data.Value.ToStringUtf8();
            json.Should().Be(JsonSerializer.Serialize(invokeRequest, jsonOptions));

            SendResponse(invokedResponse, entry, jsonOptions);
            var response = await task;

            response.Name.Should().Be(invokedResponse.Name);
        }

        private async void SendResponse<T>(T data, TestHttpClient.Entry entry, JsonSerializerOptions options = null)
        {
            var dataAny = await ProtobufUtils.ConvertToAnyAsync(data, options);
            var dataResponse = new InvokeServiceResponseEnvelope();
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
