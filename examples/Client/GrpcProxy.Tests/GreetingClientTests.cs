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

using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcServiceSample.Generated;
using Xunit;

namespace GrpcProxy.Tests;

public class GreetingClientTests
{
    [Fact]
    public async Task SayHelloAsync_RoutesGeneratedGrpcCallThroughSidecar()
    {
        using var handler = new SidecarHandler(StatusCode.OK);
        var client = new GreetingClient("http://localhost:50001", new GrpcChannelOptions { HttpHandler = handler });

        Assert.Equal("Hello Ada", await client.SayHelloAsync("Ada", TestContext.Current.CancellationToken));
        Assert.Equal("http://localhost:50001/helloworld.Greeter/SayHello", handler.RequestUri);
        Assert.Equal("grpcsample", handler.AppId);
        Assert.Equal("Ada", handler.Name);
        Assert.InRange(handler.Timeout.TotalMilliseconds, 1, 10_001);
    }

    [Fact]
    public async Task SayHelloAsync_PropagatesSidecarFailure()
    {
        using var handler = new SidecarHandler(StatusCode.Unavailable);
        var client = new GreetingClient("http://localhost:50001", new GrpcChannelOptions { HttpHandler = handler });

        var exception = await Assert.ThrowsAsync<RpcException>(() => client.SayHelloAsync("Ada", TestContext.Current.CancellationToken));

        Assert.Equal(StatusCode.Unavailable, exception.StatusCode);
    }

    [Fact]
    public async Task SayHelloAsync_CancelsInFlightRequest()
    {
        using var handler = new SidecarHandler(StatusCode.OK, waitForCancellation: true);
        var client = new GreetingClient("http://localhost:50001", new GrpcChannelOptions { HttpHandler = handler });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var call = client.SayHelloAsync("Ada", cancellation.Token);
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAsync<RpcException>(
            () => call.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.Cancelled, exception.StatusCode);
    }

    // A simulated sidecar verifies the gRPC wire request; no Dapr runtime is needed.
    private sealed class SidecarHandler(StatusCode statusCode, bool waitForCancellation = false) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string? AppId { get; private set; }
        public string? Name { get; private set; }
        public TimeSpan Timeout { get; private set; }
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri!.ToString();
            AppId = request.Headers.GetValues("dapr-app-id").Single();
            var timeout = request.Headers.GetValues("grpc-timeout").Single();
            var timeoutValue = double.Parse(timeout[..^1], System.Globalization.CultureInfo.InvariantCulture);
            Timeout = timeout[^1] switch
            {
                'H' => TimeSpan.FromHours(timeoutValue),
                'M' => TimeSpan.FromMinutes(timeoutValue),
                'S' => TimeSpan.FromSeconds(timeoutValue),
                'm' => TimeSpan.FromMilliseconds(timeoutValue),
                'u' => TimeSpan.FromMilliseconds(timeoutValue / 1_000),
                'n' => TimeSpan.FromMilliseconds(timeoutValue / 1_000_000),
                _ => throw new InvalidOperationException("Unexpected gRPC timeout unit.")
            };
            var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            Name = HelloRequest.Parser.ParseFrom(body.AsSpan(5).ToArray()).Name;
            RequestStarted.TrySetResult();
            if (waitForCancellation)
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            }

            var payload = new HelloReply { Message = "Hello " + Name }.ToByteArray();
            var frame = new byte[5 + payload.Length];
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1, 4), payload.Length);
            payload.CopyTo(frame, 5);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = HttpVersion.Version20,
                Content = new ByteArrayContent(statusCode == StatusCode.OK ? frame : []),
                RequestMessage = request
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            response.TrailingHeaders.Add("grpc-status", ((int)statusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return response;
        }
    }
}
