// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using Grpc.Core;
using Xunit;
using Xunit.Abstractions;

namespace Dapr.E2E.Test
{
    public class GrpcProxyTests : DaprTestAppLifecycle
    {
        // Grpc proxying requires a specific setup so we can't use the built-in standard for the E2ETest partial class.
        public GrpcProxyTests(ITestOutputHelper output, DaprTestAppFixture fixture) : base(output, fixture)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            base.Configuration = new DaprRunConfiguration 
            {
                UseAppPort = true,
                AppId = "grpcapp",
                AppProtocol = "grpc",
                TargetProject = "./../../../../../test/Dapr.E2E.Test.App.Grpc/Dapr.E2E.Test.App.Grpc.csproj",
            };
        }

        [Fact]
        public async Task TestGrpcProxyMessageSendAndReceive()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var invoker = DaprClient.CreateInvocationInvoker(appId: this.AppId, daprEndpoint: this.GrpcEndpoint);
            var client = new Messager.MessagerClient(invoker);

            await client.SendMessageAsync(new SendMessageRequest { Recipient = "Client", Message = "Hello"}, cancellationToken: cts.Token);

            var response = await client.GetMessageAsync(new GetMessageRequest { Recipient = "Client" }, cancellationToken: cts.Token);

            Assert.Equal("Hello", response.Message);
        }

        [Fact]
        public async Task TestGrpcProxyStreamingBroadcast()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var invoker = DaprClient.CreateInvocationInvoker(appId: this.AppId, daprEndpoint: this.GrpcEndpoint);
            var client = new Messager.MessagerClient(invoker);

            var messageCount = 10;
            var messageReceived = 0;
            using (var call = client.StreamBroadcast(cancellationToken: cts.Token)) {
                var responseTask = Task.Run(async () =>
                {
                    await foreach (var response in call.ResponseStream.ReadAllAsync())
                    {
                        Assert.Equal($"Hello: {messageReceived++}", response.Message);
                    }
                });

                for (var i = 0; i < messageCount; i++)
                {
                    await call.RequestStream.WriteAsync(new Broadcast { Message = $"Hello: {i}" });
                }
                await call.RequestStream.CompleteAsync();

                await responseTask;
            }
        }
    }
}