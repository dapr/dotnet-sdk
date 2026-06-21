// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Dapr.Client;
using Dapr.IntegrationTest.DaprClient.GrpcServices;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.DaprClient.ServiceInvocation;

/// <summary>
/// Integration tests for gRPC proxy invocation via <see cref="global::Dapr.Client.DaprClient.CreateInvocationInvoker"/>.
/// The tests host a real <see cref="EchoServiceImpl"/> inside the test process; the Dapr sidecar is
/// started with <c>--app-protocol grpc</c> so it proxies gRPC calls through to the in-process service.
/// </summary>
public sealed class GrpcProxyInvocationTests
{
    /// <summary>
    /// Verifies a successful unary gRPC call proxied through the Dapr sidecar.
    /// The sidecar receives the call on its gRPC port and forwards it to the
    /// in-process <see cref="EchoServiceImpl"/> using the gRPC app protocol.
    /// </summary>
    [Fact]
    public async Task CreateInvocationInvoker_UnarySend_ReturnsEchoedReply()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        await using var ctx = await CreateTestContextAsync();
        var client = new EchoService.EchoServiceClient(ctx.Invoker);

        var reply = await client.EchoAsync(
            new EchoRequest { Message = "hello from test" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("hello from test", reply.Message);
    }

    /// <summary>
    /// Verifies that a bidirectional-streaming gRPC call proxied through the Dapr sidecar
    /// reflects all sent messages back in the same order.
    /// </summary>
    [Fact]
    public async Task CreateInvocationInvoker_BidirectionalStreaming_EchoesAllMessages()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        await using var ctx = await CreateTestContextAsync();
        var client = new EchoService.EchoServiceClient(ctx.Invoker);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var messages = new[] { "alpha", "beta", "gamma" };
        var received = new List<string>();

        using var call = client.BidirectionalEcho(cancellationToken: cts.Token);

        // Read all responses in a background task while sending requests.
        var readerTask = Task.Run(async () =>
        {
            await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
            {
                received.Add(response.Message);
            }
        }, cts.Token);

        foreach (var msg in messages)
        {
            await call.RequestStream.WriteAsync(new EchoRequest { Message = msg }, cts.Token);
        }

        await call.RequestStream.CompleteAsync();
        await readerTask;

        Assert.Equal(messages, received);
    }

    /// <summary>
    /// Verifies that a gRPC call with a past deadline throws <see cref="RpcException"/>
    /// with <see cref="StatusCode.DeadlineExceeded"/> when proxied through the Dapr sidecar.
    /// </summary>
    [Fact]
    public async Task CreateInvocationInvoker_PastDeadline_ThrowsDeadlineExceeded()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        await using var ctx = await CreateTestContextAsync();
        var client = new EchoService.EchoServiceClient(ctx.Invoker);

        // 1-second deadline against a 5-second server handler.
        var options = new CallOptions(
            deadline: DateTime.UtcNow.AddSeconds(1),
            cancellationToken: TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await client.SlowEchoAsync(new Empty(), options);
        });

        Assert.Equal(StatusCode.DeadlineExceeded, ex.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Shared test-app / harness factory
    // -----------------------------------------------------------------------

    private sealed record HarnessContext(CallInvoker Invoker, IAsyncDisposable Inner) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await Inner.DisposeAsync();
    }

    private static async Task<HarnessContext> CreateTestContextAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("dapr-client-grpc");

        // Tell the sidecar to use gRPC to talk to our in-process service.
        var options = new DaprRuntimeOptions().WithAppProtocol("grpc");

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(options)
            .BuildServiceInvocation();

        var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddGrpc();

                // Enable cleartext HTTP/2 so the in-process Kestrel server can
                // serve gRPC without TLS, which is what daprd expects when
                // communicating over the Docker host-gateway.
                builder.WebHost.ConfigureKestrel(kestrelOptions =>
                {
                    kestrelOptions.ConfigureEndpointDefaults(ep =>
                    {
                        ep.Protocols = HttpProtocols.Http2;
                    });
                });
            })
            .ConfigureApp(app =>
            {
                app.MapGrpcService<EchoServiceImpl>();
            })
            .BuildAndStartAsync();

        var invoker = global::Dapr.Client.DaprClient.CreateInvocationInvoker(
            appId: harness.AppId,
            daprEndpoint: $"http://127.0.0.1:{harness.DaprGrpcPort}");

        return new HarnessContext(invoker, testApp);
    }
}
