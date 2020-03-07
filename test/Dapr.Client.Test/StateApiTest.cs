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

    public class StateApiTest
    {
        [Fact]
        public async Task GetStateAsync_CanReadState()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.GetStateAsync<Widget>("testStore", "test");

            // Create Response & Respond
            var data = new Widget() { Size = "small", Color = "yellow", };
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            SendResponseWithState(data, entry);

            // Get response and validate
            var state = await task;
            state.Size.Should().Be("small");
            state.Color.Should().Be("yellow");
        }

        [Fact]
        public async Task GetStateAsync_CanReadEmptyState_ReturnsDefault()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.GetStateAsync<Widget>("testStore", "test");

            // Create Response & Respond
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            SendResponseWithState<Widget>(null, entry);

            // Get response and validate
            var state = await task;
            state.Should().BeNull();
        }

        [Fact]
        public async Task GetStateAsync_ThrowsForNonSuccess()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            // Create Response & Respond
            var task = daprClient.GetStateAsync<Widget>("testStore", "test");
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var response = GrpcUtils.CreateResponse(HttpStatusCode.NotAcceptable);
            entry.Completion.SetResult(response);

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task SaveStateAsync_CanSaveState()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var widget = new Widget() { Size = "small", Color = "yellow", };
            var task = daprClient.SaveStateAsync("testStore", "test", widget);

            
            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<SaveStateEnvelope>(entry.Request);
            
            envelope.StoreName.Should().Be("testStore");
            envelope.Requests.Count.Should().Be(1);
            var request = envelope.Requests[0];
            request.Key.Should().Be("test");

            var stateJson = request.Value.Value.ToStringUtf8();
            var stateFromRequest = JsonSerializer.Deserialize<Widget>(stateJson);
            stateFromRequest.Size.Should().Be(widget.Size);
            stateFromRequest.Color.Should().Be(widget.Color);
        }

        [Fact]
        public async Task SaveStateAsync_CanClearState()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.SaveStateAsync<object>("testStore", "test", null);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<SaveStateEnvelope>(entry.Request);

            envelope.StoreName.Should().Be("testStore");
            envelope.Requests.Count.Should().Be(1);
            var request = envelope.Requests[0];
            request.Key.Should().Be("test");

            request.Value.Should().BeNull();
        }

        [Fact]
        public async Task SetStateAsync_ThrowsForNonSuccess()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();
            
            var widget = new Widget() { Size = "small", Color = "yellow", };
            var task = daprClient.SaveStateAsync("testStore", "test", widget);

            // Create Response & Respond
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var response = GrpcUtils.CreateResponse(HttpStatusCode.NotAcceptable);
            entry.Completion.SetResult(response);

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task DeleteStateAsync_CanDeleteState()
        {
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.DeleteStateAsync("testStore", "test");

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<DeleteStateEnvelope>(entry.Request);
            envelope.StoreName.Should().Be("testStore");
            envelope.Key.Should().Be("test");
        }

        [Fact]
        public async Task DeleteStateAsync_ThrowsForNonSuccess()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.DeleteStateAsync("testStore", "test");

            // Create Response & Respond
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var response = GrpcUtils.CreateResponse(HttpStatusCode.NotAcceptable);
            entry.Completion.SetResult(response);

            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task GetStateEntryAsync_CanReadState()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.GetStateEntryAsync<Widget>("testStore", "test");

            // Create Response & Respond
            var data = new Widget() { Size = "small", Color = "yellow", };
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            SendResponseWithState(data, entry);

            // Get response and validate
            var state = await task;
            state.Value.Size.Should().Be("small");
            state.Value.Color.Should().Be("yellow");
        }

        [Fact]
        public async Task GetStateEntryAsync_CanReadEmptyState_ReturnsDefault()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.GetStateEntryAsync<Widget>("testStore", "test");

            // Create Response & Respond
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            SendResponseWithState<Widget>(null, entry);

            var state = await task;
            state.Key.Should().Be("test");
            state.Value.Should().BeNull();
        }

        [Fact]
        public async Task GetStateEntryAsync_CanSaveState()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.GetStateEntryAsync<Widget>("testStore", "test");

            // Create Response & Respond
            var data = new Widget() { Size = "small", Color = "yellow", };
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            SendResponseWithState(data, entry);

            var state = await task;
            state.Key.Should().Be("test");
            state.Value.Size.Should().Be("small");
            state.Value.Color.Should().Be("yellow");

            // Modify the state and save it
            state.Value.Color = "green";
            var task2 = state.SaveAsync();

            // Get Request and validate
            httpClient.Requests.TryDequeue(out entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<SaveStateEnvelope>(entry.Request);

            envelope.StoreName.Should().Be("testStore");
            envelope.Requests.Count.Should().Be(1);
            var request = envelope.Requests[0];
            request.Key.Should().Be("test");

            var stateJson = request.Value.Value.ToStringUtf8();
            var stateFromRequest = JsonSerializer.Deserialize<Widget>(stateJson);
            stateFromRequest.Size.Should().Be("small");
            stateFromRequest.Color.Should().Be("green");
        }

        [Fact]
        public async Task GetStateEntryAsync_CanDeleteState()
        {
            // Configure client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var task = daprClient.GetStateEntryAsync<Widget>("testStore", "test");

            // Create Response & Respond
            var data = new Widget() { Size = "small", Color = "yellow", };
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            SendResponseWithState(data, entry);

            var state = await task;
            state.Key.Should().Be("test");
            state.Value.Size.Should().Be("small");
            state.Value.Color.Should().Be("yellow");

            state.Value.Color = "green";
            var task2 = state.DeleteAsync();

            // Get Request and validate
            httpClient.Requests.TryDequeue(out entry).Should().BeTrue();
            var envelope = await GrpcUtils.GetEnvelopeFromRequestMessageAsync<DeleteStateEnvelope>(entry.Request); 
            envelope.StoreName.Should().Be("testStore");
            envelope.Key.Should().Be("test");
        }

        private async void SendResponseWithState<T>(T state, TestHttpClient.Entry entry)
        {
            var stateAny = await ProtobufUtils.ConvertToAnyAsync(state);
            var stateResponse = new GetStateResponseEnvelope();
            stateResponse.Data = stateAny;
            stateResponse.Etag = "test";

            var streamContent = await GrpcUtils.CreateResponseContent(stateResponse);
            var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            entry.Completion.SetResult(response);
        }

        private class Widget
        {
            public string Size { get; set; }

            public string Color { get; set; }
        }
    }
}
