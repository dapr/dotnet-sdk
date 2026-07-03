using System.Threading.Channels;
using Dapr.Actors.Next.Core.Transport;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Testing;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.Test;

public sealed class GeneratedActorEventsTransportTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Transport_maps_generated_actor_stream_messages()
    {
        var harness = new GeneratedActorEventsHarness();
        var transport = new DaprActorEventsTransport(harness.Client);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var stream = await transport.OpenStreamAsync(cts.Token);
        await stream.WriteAsync(SubscribeActorEventsResponse.RegisteredActors(["NoConfig"]), cts.Token);
        var noConfigInitial = await harness.ReceiveAsync(cts.Token);
        await stream.WriteAsync(SubscribeActorEventsResponse.RegisteredActors(
            ["Counter"],
            new SubscribeActorEventsInitialConfig(
                TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(30),
                true,
                true,
                16)), cts.Token);
        var initial = await harness.ReceiveAsync(cts.Token);
        await harness.SendAsync(new P.SubscribeActorEventsResponseAlpha1
        {
            InvokeRequest = new P.SubscribeActorEventsResponseInvokeRequestAlpha1
            {
                Id = "invoke-1",
                ActorType = "Counter",
                ActorId = "one",
                Method = "Increment",
                Data = ByteString.CopyFromUtf8("3"),
                Metadata = { ["traceparent"] = "tp" },
            },
        }, cts.Token);

        await using var reader = stream.ReadAllAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        Assert.True(await reader.MoveNextAsync());
        var request = reader.Current;
        await stream.WriteAsync(
            new SubscribeActorEventsResponse(
                request.Id,
                request.Kind,
                System.Text.Encoding.UTF8.GetBytes("ok"),
                new Dictionary<string, string> { ["content-type"] = "text/plain" }),
            cts.Token);
        var response = await harness.ReceiveAsync(cts.Token);

        Assert.Equal(new[] { "NoConfig" }, noConfigInitial.InitialRequest.Entities.ToArray());
        Assert.Null(noConfigInitial.InitialRequest.Reentrancy);
        Assert.Equal(new[] { "Counter" }, initial.InitialRequest.Entities.ToArray());
        var entityConfig = Assert.Single(initial.InitialRequest.EntitiesConfig);
        Assert.Equal(new[] { "Counter" }, entityConfig.Entities.ToArray());
        Assert.Equal(60, entityConfig.ActorIdleTimeout.Seconds);
        Assert.Equal(30, entityConfig.DrainOngoingCallTimeout.Seconds);
        Assert.True(entityConfig.DrainRebalancedActors);
        Assert.True(entityConfig.Reentrancy.Enabled);
        Assert.Equal(16, entityConfig.Reentrancy.MaxStackDepth);
        Assert.Equal("invoke-1", request.Id);
        Assert.Equal(SubscribeActorEventsFrameKind.Invoke, request.Kind);
        Assert.Equal("Counter", request.ActorType);
        Assert.Equal("one", request.ActorId);
        Assert.Equal("Increment", request.MethodName);
        Assert.Equal("3", System.Text.Encoding.UTF8.GetString(request.Payload.Span));
        Assert.Equal("tp", request.Headers["traceparent"]);
        Assert.Equal("invoke-1", response.InvokeResponse.Id);
        Assert.Equal("ok", response.InvokeResponse.Data.ToStringUtf8());
        Assert.Equal("text/plain", response.InvokeResponse.Metadata["content-type"]);
        Assert.False(response.InvokeResponse.Error);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Transport_maps_failures_and_non_invoke_callbacks()
    {
        var harness = new GeneratedActorEventsHarness();
        var transport = new DaprActorEventsTransport(harness.Client);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var stream = await transport.OpenStreamAsync(cts.Token);
        await harness.SendAsync(new P.SubscribeActorEventsResponseAlpha1
        {
            InitialResponse = new P.SubscribeActorEventsResponseInitialAlpha1(),
        }, cts.Token);
        await harness.SendAsync(new P.SubscribeActorEventsResponseAlpha1
        {
            ReminderRequest = new P.SubscribeActorEventsResponseReminderRequestAlpha1
            {
                Id = "reminder-1",
                ActorType = "Counter",
                ActorId = "one",
                Name = "Wake",
                DueTime = "1s",
                Period = "2s",
                Data = new Google.Protobuf.WellKnownTypes.Any { TypeUrl = "type.googleapis.com/test.Payload", Value = ByteString.CopyFromUtf8("wake") },
            },
        }, cts.Token);
        await harness.SendAsync(new P.SubscribeActorEventsResponseAlpha1
        {
            TimerRequest = new P.SubscribeActorEventsResponseTimerRequestAlpha1
            {
                Id = "timer-1",
                ActorType = "Counter",
                ActorId = "one",
                Name = "timer",
                DueTime = "3s",
                Period = "4s",
                Callback = "Tick",
                Data = new Google.Protobuf.WellKnownTypes.Any { TypeUrl = "type.googleapis.com/test.Timer", Value = ByteString.CopyFromUtf8("payload") },
            },
        }, cts.Token);
        await harness.SendAsync(new P.SubscribeActorEventsResponseAlpha1
        {
            TimerRequest = new P.SubscribeActorEventsResponseTimerRequestAlpha1
            {
                Id = "timer-2",
                ActorType = "Counter",
                ActorId = "one",
                Name = "Fallback",
            },
        }, cts.Token);
        await harness.SendAsync(new P.SubscribeActorEventsResponseAlpha1
        {
            DeactivateRequest = new P.SubscribeActorEventsResponseDeactivateRequestAlpha1
            {
                Id = "deactivate-1",
                ActorType = "Counter",
                ActorId = "one",
            },
        }, cts.Token);

        await using var reader = stream.ReadAllAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        Assert.True(await reader.MoveNextAsync());
        var reminder = reader.Current;
        Assert.True(await reader.MoveNextAsync());
        var request = reader.Current;
        Assert.True(await reader.MoveNextAsync());
        var fallbackTimer = reader.Current;
        Assert.True(await reader.MoveNextAsync());
        var deactivate = reader.Current;
        await stream.WriteAsync(new SubscribeActorEventsResponse(reminder.Id, reminder.Kind, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(), Cancel: true), cts.Token);
        var reminderAck = await harness.ReceiveAsync(cts.Token);
        await stream.WriteAsync(new SubscribeActorEventsResponse(request.Id, request.Kind, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(), "bad callback"), cts.Token);
        var failed = await harness.ReceiveAsync(cts.Token);
        await stream.WriteAsync(new SubscribeActorEventsResponse("invoke-error", SubscribeActorEventsFrameKind.Invoke, ByteString.CopyFromUtf8("bad app").ToByteArray(), new Dictionary<string, string>(), Error: true), cts.Token);
        var appError = await harness.ReceiveAsync(cts.Token);
        await stream.WriteAsync(new SubscribeActorEventsResponse(fallbackTimer.Id, fallbackTimer.Kind, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(), Cancel: true), cts.Token);
        var timerAck = await harness.ReceiveAsync(cts.Token);
        await stream.WriteAsync(new SubscribeActorEventsResponse(deactivate.Id, deactivate.Kind, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()), cts.Token);
        var deactivateAck = await harness.ReceiveAsync(cts.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await stream.WriteAsync(new SubscribeActorEventsResponse("bad-kind", (SubscribeActorEventsFrameKind)999, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()), cts.Token));

        Assert.Equal(SubscribeActorEventsFrameKind.Reminder, reminder.Kind);
        Assert.Equal("Wake", reminder.MethodName);
        Assert.Equal("wake", System.Text.Encoding.UTF8.GetString(reminder.Payload.Span));
        Assert.Equal("1s", reminder.Headers["dapr-due-time"]);
        Assert.Equal("2s", reminder.Headers["dapr-period"]);
        Assert.Equal("type.googleapis.com/test.Payload", reminder.Headers["content-type"]);
        Assert.Equal(SubscribeActorEventsFrameKind.Timer, request.Kind);
        Assert.Equal("Tick", request.MethodName);
        Assert.Equal("payload", System.Text.Encoding.UTF8.GetString(request.Payload.Span));
        Assert.Equal("3s", request.Headers["dapr-due-time"]);
        Assert.Equal("4s", request.Headers["dapr-period"]);
        Assert.Equal("type.googleapis.com/test.Timer", request.Headers["content-type"]);
        Assert.Equal("Fallback", fallbackTimer.MethodName);
        Assert.Empty(fallbackTimer.Payload.ToArray());
        Assert.Equal(SubscribeActorEventsFrameKind.Deactivate, deactivate.Kind);
        Assert.Equal(string.Empty, deactivate.MethodName);
        Assert.Equal("reminder-1", reminderAck.ReminderResponse.Id);
        Assert.True(reminderAck.ReminderResponse.Cancel);
        Assert.Equal("timer-1", failed.RequestFailed.Id);
        Assert.Equal("bad callback", failed.RequestFailed.Message);
        Assert.Equal("invoke-error", appError.InvokeResponse.Id);
        Assert.True(appError.InvokeResponse.Error);
        Assert.Equal("bad app", appError.InvokeResponse.Data.ToStringUtf8());
        Assert.Equal("timer-2", timerAck.TimerResponse.Id);
        Assert.True(timerAck.TimerResponse.Cancel);
        Assert.Equal("deactivate-1", deactivateAck.DeactivateResponse.Id);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Transport_adds_dapr_api_token_to_stream_call_options()
    {
        var harness = new GeneratedActorEventsHarness();
        var transport = new DaprActorEventsTransport(harness.Client, "test-token");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var stream = await transport.OpenStreamAsync(cts.Token);

        Assert.Contains(harness.StreamCallOptions.Headers!, entry => entry.Key == "dapr-api-token" && entry.Value == "test-token");
        Assert.Contains(harness.StreamCallOptions.Headers!, entry => entry.Key == "user-agent");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Transport_explains_http2_protocol_mismatch_on_initial_write()
    {
        var harness = new ProtocolMismatchHarness();
        var transport = new DaprActorEventsTransport(
            new Lazy<P.Dapr.DaprClient>(() => harness.Client),
            daprGrpcEndpoint: "http://127.0.0.1:3500");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var stream = await transport.OpenStreamAsync(cts.Token);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await stream.WriteAsync(SubscribeActorEventsResponse.RegisteredActors(["Cart"]), cts.Token));

        Assert.Contains("http://127.0.0.1:3500", ex.Message);
        Assert.Contains("DAPR_GRPC_ENDPOINT", ex.Message);
        Assert.Contains("--app-protocol grpc", ex.Message);
        Assert.IsType<RpcException>(ex.InnerException);
    }

    private sealed class ProtocolMismatchHarness
    {
        private readonly Channel<P.SubscribeActorEventsResponseAlpha1> responses = System.Threading.Channels.Channel.CreateUnbounded<P.SubscribeActorEventsResponseAlpha1>();

        public P.Dapr.DaprClient Client { get; }

        public ProtocolMismatchHarness()
        {
            Client = new TestDaprClient(this);
        }

        private sealed class TestDaprClient(ProtocolMismatchHarness owner) : P.Dapr.DaprClient
        {
            public override AsyncDuplexStreamingCall<P.SubscribeActorEventsRequestAlpha1, P.SubscribeActorEventsResponseAlpha1> SubscribeActorEventsAlpha1(CallOptions options)
            {
                return TestCalls.AsyncDuplexStreamingCall(
                    new FailingRequestWriter(),
                    new GeneratedActorEventsHarness.ResponseReader(owner.responses.Reader),
                    Task.FromResult(new Metadata()),
                    () => new Status(StatusCode.Internal, "Error starting gRPC call."),
                    () => new Metadata(),
                    () => owner.responses.Writer.TryComplete());
            }
        }

        private sealed class FailingRequestWriter : IClientStreamWriter<P.SubscribeActorEventsRequestAlpha1>
        {
            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(P.SubscribeActorEventsRequestAlpha1 message) =>
                WriteAsync(message, CancellationToken.None);

            public Task WriteAsync(P.SubscribeActorEventsRequestAlpha1 message, CancellationToken cancellationToken) =>
                Task.FromException(new RpcException(new Status(
                    StatusCode.Internal,
                    "Error starting gRPC call. HttpRequestException: The HTTP/2 server sent invalid data on the connection. HTTP/2 error code 'PROTOCOL_ERROR' (0x1).")));

            public Task CompleteAsync() => Task.CompletedTask;
        }
    }

    private sealed class GeneratedActorEventsHarness
    {
        private readonly Channel<P.SubscribeActorEventsRequestAlpha1> requests = System.Threading.Channels.Channel.CreateUnbounded<P.SubscribeActorEventsRequestAlpha1>();
        private readonly Channel<P.SubscribeActorEventsResponseAlpha1> responses = System.Threading.Channels.Channel.CreateUnbounded<P.SubscribeActorEventsResponseAlpha1>();

        public P.Dapr.DaprClient Client { get; }

        public CallOptions StreamCallOptions { get; private set; }

        public GeneratedActorEventsHarness()
        {
            Client = new TestDaprClient(this);
        }

        public ValueTask SendAsync(P.SubscribeActorEventsResponseAlpha1 response, CancellationToken cancellationToken) =>
            responses.Writer.WriteAsync(response, cancellationToken);

        public ValueTask<P.SubscribeActorEventsRequestAlpha1> ReceiveAsync(CancellationToken cancellationToken) =>
            requests.Reader.ReadAsync(cancellationToken);

        private sealed class TestDaprClient(GeneratedActorEventsHarness owner) : P.Dapr.DaprClient
        {
            public override AsyncDuplexStreamingCall<P.SubscribeActorEventsRequestAlpha1, P.SubscribeActorEventsResponseAlpha1> SubscribeActorEventsAlpha1(CallOptions options)
            {
                owner.StreamCallOptions = options;
                return TestCalls.AsyncDuplexStreamingCall(
                    new RequestWriter(owner.requests.Writer),
                    new ResponseReader(owner.responses.Reader),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () =>
                    {
                        owner.requests.Writer.TryComplete();
                        owner.responses.Writer.TryComplete();
                    });
            }
        }

        private sealed class RequestWriter(ChannelWriter<P.SubscribeActorEventsRequestAlpha1> writer) : IClientStreamWriter<P.SubscribeActorEventsRequestAlpha1>
        {
            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(P.SubscribeActorEventsRequestAlpha1 message) =>
                writer.WriteAsync(message).AsTask();

            public Task WriteAsync(P.SubscribeActorEventsRequestAlpha1 message, CancellationToken cancellationToken) =>
                writer.WriteAsync(message, cancellationToken).AsTask();

            public Task CompleteAsync()
            {
                writer.TryComplete();
                return Task.CompletedTask;
            }
        }

        public sealed class ResponseReader(ChannelReader<P.SubscribeActorEventsResponseAlpha1> reader) : IAsyncStreamReader<P.SubscribeActorEventsResponseAlpha1>
        {
            public P.SubscribeActorEventsResponseAlpha1 Current { get; private set; } = new();

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                while (await reader.WaitToReadAsync(cancellationToken))
                {
                    if (reader.TryRead(out var item))
                    {
                        Current = item;
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
