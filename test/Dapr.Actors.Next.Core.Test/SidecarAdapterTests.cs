using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Common.Serialization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Core.Testing;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.Test;

public sealed class SidecarAdapterTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Invocation_client_maps_actor_invoke_request_and_response()
    {
        var client = new RecordingDaprClient { InvokeActorResponse = new P.InvokeActorResponse { Data = ByteString.CopyFromUtf8("ok") } };
        var invocation = new DaprActorInvocationClient(client);

        var response = await invocation.InvokeAsync(
            "Counter",
            "one",
            "Increment",
            ByteString.CopyFromUtf8("1").Memory,
            new Dictionary<string, string> { ["content-type"] = "application/json" });

        Assert.Equal("ok", System.Text.Encoding.UTF8.GetString(response!));
        Assert.Equal("Counter", client.InvokeActorRequest!.ActorType);
        Assert.Equal("one", client.InvokeActorRequest.ActorId);
        Assert.Equal("Increment", client.InvokeActorRequest.Method);
        Assert.Equal("1", client.InvokeActorRequest.Data.ToStringUtf8());
        Assert.Equal("application/json", client.InvokeActorRequest.Metadata["content-type"]);
        client.InvokeActorResponse = new P.InvokeActorResponse();
        Assert.Null(await invocation.InvokeAsync("Counter", "one", "Read", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()));
        await Assert.ThrowsAsync<ArgumentException>(async () => await invocation.InvokeAsync("", "one", "Read", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Sidecar_adapters_add_dapr_api_token_to_call_options()
    {
        var client = new RecordingDaprClient { InvokeActorResponse = new P.InvokeActorResponse() };
        var invocation = new DaprActorInvocationClient(client, "test-token");
        var store = new DaprSidecarActorStateStore(client, "actors", "test-token");
        var scheduler = new DaprSidecarActorTimerScheduler(client, new ActorWireSerializer(new JsonDaprSerializer()), "test-token");
        var actorId = ActorId.Create("one");

        await invocation.InvokeAsync("Counter", actorId.Value, "Ping", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>());
        await store.ReadAsync("Counter", actorId.Value, "state");
        await scheduler.ScheduleAsync("Counter", actorId, "tick", TimeSpan.Zero, "Tick", "0");

        AssertToken(client.InvokeActorOptions);
        AssertToken(client.GetStateOptions);
        AssertToken(client.RegisterActorTimerOptions);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task State_store_maps_sidecar_state_requests()
    {
        var client = new RecordingDaprClient { GetStateResponse = new P.GetStateResponse { Data = ByteString.CopyFromUtf8("value") } };
        var store = new DaprSidecarActorStateStore(client, "actors");

        var value = await store.ReadAsync("Counter Type", "id/1", "state name");
        await store.WriteAsync("Counter Type", "id/1", "state name", ByteString.CopyFromUtf8("next").Memory);
        await store.DeleteAsync("Counter Type", "id/1", "state name");
        client.DeleteNotFound = true;
        await store.DeleteAsync("Counter Type", "id/1", "state name");
        client.GetStateResponse = new P.GetStateResponse();

        Assert.Equal("value", System.Text.Encoding.UTF8.GetString(value!.Value.Span));
        Assert.Equal("actors", client.GetStateRequest!.StoreName);
        Assert.Equal("actors-next:Counter%20Type:id%2F1:state%20name", client.GetStateRequest.Key);
        Assert.Equal("next", client.SaveStateRequest!.States.Single().Value.ToStringUtf8());
        Assert.Equal(client.GetStateRequest.Key, client.SaveStateRequest.States.Single().Key);
        Assert.Equal(client.GetStateRequest.Key, client.DeleteStateRequest!.Key);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task State_store_treats_sidecar_json_null_as_missing_state()
    {
        var client = new RecordingDaprClient { GetStateResponse = new P.GetStateResponse { Data = ByteString.CopyFromUtf8("null") } };
        var store = new DaprSidecarActorStateStore(client, "actors");

        var value = await store.ReadAsync("Counter", "missing", "state");

        Assert.Null(value);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Timer_scheduler_maps_sidecar_timer_requests()
    {
        var client = new RecordingDaprClient();
        var scheduler = new DaprSidecarActorTimerScheduler(client, new ActorWireSerializer(new JsonDaprSerializer()));
        var actorId = ActorId.Create("timer");

        await scheduler.ScheduleAsync("Counter", actorId, "tick", TimeSpan.FromMilliseconds(250), "Tick", """{"x":1}""");
        await scheduler.RescheduleAsync("Counter", actorId, "tick", TimeSpan.Zero, "Tick", "0");
        await scheduler.CancelAsync("Counter", actorId, "tick");

        Assert.Equal("Counter", client.RegisterActorTimerRequest!.ActorType);
        Assert.Equal("timer", client.RegisterActorTimerRequest.ActorId);
        Assert.Equal("tick", client.RegisterActorTimerRequest.Name);
        Assert.Equal("0ms", client.RegisterActorTimerRequest.DueTime);
        Assert.Equal("100ms", client.RegisterActorTimerRequest.Period);
        Assert.Equal("Tick", client.RegisterActorTimerRequest.Callback);
        Assert.Equal("0", client.RegisterActorTimerRequest.Data.ToStringUtf8());
        Assert.Equal("tick", client.UnregisterActorTimerRequest!.Name);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await scheduler.ScheduleAsync("Counter", actorId, "bad", TimeSpan.FromMilliseconds(-1), "Tick", "0"));
    }

    private sealed class RecordingDaprClient : P.Dapr.DaprClient
    {
        public P.InvokeActorRequest? InvokeActorRequest { get; private set; }

        public CallOptions InvokeActorOptions { get; private set; }

        public P.InvokeActorResponse InvokeActorResponse { get; set; } = new();

        public P.GetStateRequest? GetStateRequest { get; private set; }

        public CallOptions GetStateOptions { get; private set; }

        public P.GetStateResponse GetStateResponse { get; set; } = new();

        public P.SaveStateRequest? SaveStateRequest { get; private set; }

        public CallOptions SaveStateOptions { get; private set; }

        public P.DeleteStateRequest? DeleteStateRequest { get; private set; }

        public CallOptions DeleteStateOptions { get; private set; }

        public bool DeleteNotFound { get; set; }

        public P.RegisterActorTimerRequest? RegisterActorTimerRequest { get; private set; }

        public CallOptions RegisterActorTimerOptions { get; private set; }

        public P.UnregisterActorTimerRequest? UnregisterActorTimerRequest { get; private set; }

        public CallOptions UnregisterActorTimerOptions { get; private set; }

        public override AsyncUnaryCall<P.InvokeActorResponse> InvokeActorAsync(P.InvokeActorRequest request, CallOptions options)
        {
            InvokeActorRequest = request;
            InvokeActorOptions = options;
            return Unary(InvokeActorResponse);
        }

        public override AsyncUnaryCall<P.GetStateResponse> GetStateAsync(P.GetStateRequest request, CallOptions options)
        {
            GetStateRequest = request;
            GetStateOptions = options;
            return Unary(GetStateResponse);
        }

        public override AsyncUnaryCall<Empty> SaveStateAsync(P.SaveStateRequest request, CallOptions options)
        {
            SaveStateRequest = request;
            SaveStateOptions = options;
            return Unary(new Empty());
        }

        public override AsyncUnaryCall<Empty> DeleteStateAsync(P.DeleteStateRequest request, CallOptions options)
        {
            DeleteStateRequest = request;
            DeleteStateOptions = options;
            return DeleteNotFound
                ? UnaryFailed<Empty>(new RpcException(new Status(StatusCode.NotFound, "missing")))
                : Unary(new Empty());
        }

        public override AsyncUnaryCall<Empty> RegisterActorTimerAsync(P.RegisterActorTimerRequest request, CallOptions options)
        {
            RegisterActorTimerRequest = request;
            RegisterActorTimerOptions = options;
            return Unary(new Empty());
        }

        public override AsyncUnaryCall<Empty> UnregisterActorTimerAsync(P.UnregisterActorTimerRequest request, CallOptions options)
        {
            UnregisterActorTimerRequest = request;
            UnregisterActorTimerOptions = options;
            return Unary(new Empty());
        }

        private static AsyncUnaryCall<T> Unary<T>(T response) =>
            TestCalls.AsyncUnaryCall(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                static () => Status.DefaultSuccess,
                static () => new Metadata(),
                static () => { });

        private static AsyncUnaryCall<T> UnaryFailed<T>(Exception exception) =>
            TestCalls.AsyncUnaryCall<T>(
                Task.FromException<T>(exception),
                Task.FromResult(new Metadata()),
                static () => new Status(StatusCode.Unknown, "failed"),
                static () => new Metadata(),
                static () => { });
    }

    private static void AssertToken(CallOptions options)
    {
        Assert.Contains(options.Headers!, entry => entry.Key == "dapr-api-token" && entry.Value == "test-token");
        Assert.Contains(options.Headers!, entry => entry.Key == "user-agent");
    }
}
