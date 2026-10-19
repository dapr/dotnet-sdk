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

using System.Collections.Concurrent;
using Dapr.Client;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Dapr.IntegrationTest.ServiceInvocation;

public sealed class GrpcProxyInvocationTests
{
    [Fact]
    public async Task UnaryMessagesAreProxied()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var context = await CreateGrpcAppAsync(cts.Token);
        var client = CreateClient(context);

        await client.SendMessageAsync(
            new SendMessageRequest { Recipient = "Client", Message = "Hello" },
            cancellationToken: cts.Token);
        var response = await client.GetMessageAsync(
            new GetMessageRequest { Recipient = "Client" },
            cancellationToken: cts.Token);

        Assert.Equal("Hello", response.Message);
    }

    [Fact]
    public async Task BidirectionalStreamsAreProxied()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var context = await CreateGrpcAppAsync(cts.Token);
        var client = CreateClient(context);

        using var call = client.StreamBroadcast(cancellationToken: cts.Token);
        var responses = Task.Run(async () =>
        {
            var received = new List<string>();
            await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
                received.Add(response.Message);
            return received;
        }, cts.Token);

        for (var index = 0; index < 10; index++)
            await call.RequestStream.WriteAsync(new Broadcast { Message = $"Hello: {index}" }, cts.Token);
        await call.RequestStream.CompleteAsync();

        Assert.Equal(
            Enumerable.Range(0, 10).Select(index => $"Hello: {index}"),
            await responses);
    }

    [Fact]
    public async Task DeadlinesArePropagated()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var context = await CreateGrpcAppAsync(cts.Token);
        var client = CreateClient(context);
        var options = new CallOptions(cancellationToken: cts.Token, deadline: DateTime.UtcNow.AddSeconds(1));

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.DelayedResponseAsync(new Empty(), options));

        Assert.Equal(StatusCode.DeadlineExceeded, exception.StatusCode);
    }

    private static Messager.MessagerClient CreateClient(ServiceInvocationContext context)
    {
        var invoker = DaprClient.CreateInvocationInvoker(
            appId: context.AppId,
            daprEndpoint: context.Harness.DaprGrpcEndpoint);
        return new Messager.MessagerClient(invoker);
    }

    private static async Task<ServiceInvocationContext> CreateGrpcAppAsync(CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("grpc-proxy-components");
        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var appId = $"grpc-invocation-{Guid.NewGuid():N}";
        var options = new DaprRuntimeOptions()
            .WithAppId(appId)
            .WithAppProtocol("grpc");
        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .WithOptions(options)
            .BuildServiceInvocation();

        var app = await DaprHarnessBuilder.ForHarness(harness)
            .WithDaprStartupOrder(false)
            .ConfigureServices(builder =>
            {
                builder.WebHost.ConfigureKestrel(kestrel =>
                    kestrel.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http2));
                builder.Services.AddGrpc();
                builder.Services.AddSingleton<MessageRepository>();
            })
            .ConfigureApp(webApp => webApp.MapGrpcService<MessagerService>())
            .BuildAndStartAsync();

        return new ServiceInvocationContext(environment, harness, app, appId);
    }
}

internal sealed class MessageRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> messages = new();

    public void Add(string recipient, string message) =>
        messages.GetOrAdd(recipient, _ => new ConcurrentQueue<string>()).Enqueue(message);

    public string Take(string recipient) =>
        messages.TryGetValue(recipient, out var queue) && queue.TryDequeue(out var message)
            ? message
            : string.Empty;
}

internal sealed class MessagerService(MessageRepository repository) : Messager.MessagerBase
{
    public override Task<Empty> SendMessage(SendMessageRequest request, ServerCallContext context)
    {
        repository.Add(request.Recipient, request.Message);
        return Task.FromResult(new Empty());
    }

    public override Task<MessageResponse> GetMessage(GetMessageRequest request, ServerCallContext context) =>
        Task.FromResult(new MessageResponse { Message = repository.Take(request.Recipient) });

    public override async Task StreamBroadcast(
        IAsyncStreamReader<Broadcast> requestStream,
        IServerStreamWriter<MessageResponse> responseStream,
        ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            await responseStream.WriteAsync(new MessageResponse { Message = request.Message });
    }

    public override async Task<Empty> DelayedResponse(Empty request, ServerCallContext context)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), context.CancellationToken);
        return new Empty();
    }
}
