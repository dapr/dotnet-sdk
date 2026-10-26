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
using System.Threading.Channels;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Transport;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Core.Test;

public sealed class ActorStreamFaultTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Filtered_write_fault_is_one_shot_and_does_not_consume_read_fault(bool transient)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var inner = new ChannelTransport();
        var faults = new ActorFaults();
        var transport = new FaultInjectingActorEventsTransport(inner, faults);
        await using var stream = await transport.OpenStreamAsync(timeout.Token);
        var raw = await inner.Opened.Reader.ReadAsync(timeout.Token);
        faults.FailNextStreamRead(SubscribeActorEventsFrameKind.Invoke, transient: !transient);
        faults.FailNextStreamWrite(SubscribeActorEventsFrameKind.Invoke, transient);

        var unselected = Response("timer", SubscribeActorEventsFrameKind.Timer);
        await stream.WriteAsync(unselected, timeout.Token);
        Assert.Same(unselected, await raw.Responses.Reader.ReadAsync(timeout.Token));
        var error = await Record.ExceptionAsync(() =>
            stream.WriteAsync(Response("lost", SubscribeActorEventsFrameKind.Invoke), timeout.Token).AsTask());
        AssertFault(error, transient, "write");
        Assert.False(raw.Responses.Reader.TryRead(out _));

        var next = Response("next", SubscribeActorEventsFrameKind.Invoke);
        await stream.WriteAsync(next, timeout.Token);
        Assert.Same(next, await raw.Responses.Reader.ReadAsync(timeout.Token));
        await raw.Requests.Writer.WriteAsync(Request("read", SubscribeActorEventsFrameKind.Invoke), timeout.Token);
        await using var reader = stream.ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        AssertFault(await Record.ExceptionAsync(() => reader.MoveNextAsync().AsTask()), !transient, "read");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Filtered_read_fault_consumes_only_matching_frame_and_preserves_remaining_frames(bool transient)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var inner = new ChannelTransport();
        var faults = new ActorFaults();
        var transport = new FaultInjectingActorEventsTransport(inner, faults);
        await using var stream = await transport.OpenStreamAsync(timeout.Token);
        var raw = await inner.Opened.Reader.ReadAsync(timeout.Token);
        faults.FailNextStreamRead(SubscribeActorEventsFrameKind.Invoke, transient);

        var outbound = Response("opposite-direction", SubscribeActorEventsFrameKind.Invoke);
        await stream.WriteAsync(outbound, timeout.Token);
        Assert.Same(outbound, await raw.Responses.Reader.ReadAsync(timeout.Token));
        var before = Request("before", SubscribeActorEventsFrameKind.Timer);
        var after = Request("after", SubscribeActorEventsFrameKind.Reminder);
        var next = Request("next", SubscribeActorEventsFrameKind.Invoke);
        foreach (var frame in new[] { before, Request("lost", SubscribeActorEventsFrameKind.Invoke), after, next })
        {
            await raw.Requests.Writer.WriteAsync(frame, timeout.Token);
        }
        raw.Requests.Writer.TryComplete();

        await using (var reader = stream.ReadAllAsync(timeout.Token).GetAsyncEnumerator())
        {
            Assert.True(await reader.MoveNextAsync());
            Assert.Same(before, reader.Current);
            AssertFault(await Record.ExceptionAsync(() => reader.MoveNextAsync().AsTask()), transient, "read");
        }

        await using var remaining = stream.ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        Assert.True(await remaining.MoveNextAsync());
        Assert.Same(after, remaining.Current);
        Assert.True(await remaining.MoveNextAsync());
        Assert.Same(next, remaining.Current);
        Assert.False(await remaining.MoveNextAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unfiltered_fault_targets_next_frame_once(bool write)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var inner = new ChannelTransport();
        var faults = new ActorFaults();
        await using var stream = await new FaultInjectingActorEventsTransport(inner, faults).OpenStreamAsync(timeout.Token);
        var raw = await inner.Opened.Reader.ReadAsync(timeout.Token);
        if (write)
        {
            faults.FailNextStreamWrite();
            AssertFault(await Record.ExceptionAsync(() =>
                stream.WriteAsync(Response("lost", SubscribeActorEventsFrameKind.Timer), timeout.Token).AsTask()), true, "write");
            var next = Response("next", SubscribeActorEventsFrameKind.Timer);
            await stream.WriteAsync(next, timeout.Token);
            Assert.Same(next, await raw.Responses.Reader.ReadAsync(timeout.Token));
        }
        else
        {
            faults.FailNextStreamRead();
            await raw.Requests.Writer.WriteAsync(Request("lost", SubscribeActorEventsFrameKind.Timer), timeout.Token);
            var next = Request("next", SubscribeActorEventsFrameKind.Timer);
            await raw.Requests.Writer.WriteAsync(next, timeout.Token);
            await using (var reader = stream.ReadAllAsync(timeout.Token).GetAsyncEnumerator())
            {
                AssertFault(await Record.ExceptionAsync(() => reader.MoveNextAsync().AsTask()), true, "read");
            }
            await using var remaining = stream.ReadAllAsync(timeout.Token).GetAsyncEnumerator();
            Assert.True(await remaining.MoveNextAsync());
            Assert.Same(next, remaining.Current);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Disposal_awaits_inner_stream_and_propagates_its_failure(bool fail)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var inner = new ChannelTransport();
        var stream = await new FaultInjectingActorEventsTransport(inner, new ActorFaults()).OpenStreamAsync(timeout.Token);
        var raw = await inner.Opened.Reader.ReadAsync(timeout.Token);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = fail ? new IOException("Inner disposal failure") : null;
        raw.DisposalGate = release.Task;
        raw.DisposalError = expected;
        var disposal = stream.DisposeAsync().AsTask();
        try
        {
            await raw.DisposalStarted.Task.WaitAsync(timeout.Token);
            Assert.False(disposal.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        var error = await Record.ExceptionAsync(() => disposal.WaitAsync(timeout.Token));
        Assert.Same(expected, error);
        Assert.Equal(1, raw.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Manager_reconnects_after_real_wrapper_injects_read_or_write_failure(bool write)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var inner = new ChannelTransport();
        var faults = new ActorFaults();
        if (write)
        {
            faults.FailNextStreamWrite(SubscribeActorEventsFrameKind.Invoke);
        }
        else
        {
            faults.FailNextStreamRead(SubscribeActorEventsFrameKind.Invoke);
        }
        var transport = new FaultInjectingActorEventsTransport(inner, faults);
        var dispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryActorAdapters();
        services.AddDaprActorsCore(registrations => registrations.Add(
            "FaultActor", typeof(IFaultActor), typeof(FaultActor), (_, _) => new FaultActor(), dispatcher));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var clock = new ReconnectClock();
        using var manager = new SubscribeActorEventsStreamManager(
            transport, provider.GetRequiredService<ActorRuntimeRegistry>(), provider.GetRequiredService<IActorRuntime>(),
            clock, Options.Create(new DaprActorsOptions()), NullLogger<SubscribeActorEventsStreamManager>.Instance);
        try
        {
            await manager.StartAsync(timeout.Token);
            var first = await inner.Opened.Reader.ReadAsync(timeout.Token);
            Assert.Equal(SubscribeActorEventsFrameKind.RegisteredActors,
                (await first.Responses.Reader.ReadAsync(timeout.Token)).Kind);
            await first.Requests.Writer.WriteAsync(Request("lost", SubscribeActorEventsFrameKind.Invoke), timeout.Token);
            await first.Disposed.Task.WaitAsync(timeout.Token);
            Assert.True(first.CallCancellation.IsCancellationRequested);
            Assert.False(first.Responses.Reader.TryRead(out _));
            Assert.Equal(write ? 1 : 0, dispatcher.Invocations.Count);
            (await clock.Delays.Reader.ReadAsync(timeout.Token)).Fire();

            var second = await inner.Opened.Reader.ReadAsync(timeout.Token);
            Assert.Equal(SubscribeActorEventsFrameKind.RegisteredActors,
                (await second.Responses.Reader.ReadAsync(timeout.Token)).Kind);
            await second.Requests.Writer.WriteAsync(Request("healthy", SubscribeActorEventsFrameKind.Invoke), timeout.Token);
            var response = await second.Responses.Reader.ReadAsync(timeout.Token);
            Assert.Equal("healthy", response.Id);
            Assert.False(response.Error);
            Assert.Null(response.FailureMessage);
            Assert.Equal(write ? 2 : 1, dispatcher.Invocations.Count);
        }
        finally
        {
            await manager.StopAsync(timeout.Token);
        }
    }

    private static void AssertFault(Exception? error, bool transient, string direction)
    {
        if (transient)
        {
            Assert.IsType<ActorInjectedTransientException>(error);
        }
        else
        {
            Assert.IsType<ActorInjectedPermanentException>(error);
        }
        Assert.Equal($"Injected actor stream {direction} failure.", error!.Message);
    }

    private static SubscribeActorEventsRequest Request(string id, SubscribeActorEventsFrameKind kind) =>
        new(id, kind, "FaultActor", "one", "Run", "1"u8.ToArray(),
            new Dictionary<string, string> { ["traceparent"] = "preserved", ["Dapr-Reentrancy-Id"] = id });

    private static SubscribeActorEventsResponse Response(string id, SubscribeActorEventsFrameKind kind) =>
        new(id, kind, "1"u8.ToArray(), new Dictionary<string, string> { ["custom"] = "preserved" });

    private interface IFaultActor : IActor;
    private sealed class FaultActor : IFaultActor;

    private sealed class RecordingDispatcher : IActorDispatcher
    {
        public ConcurrentQueue<string> Invocations { get; } = new();
        public ValueTask<ActorDispatchResponse> DispatchAsync(
            IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            Invocations.Enqueue(request.MethodName);
            return ValueTask.FromResult(new ActorDispatchResponse("1"u8.ToArray()));
        }
    }

    private sealed class ChannelTransport : ISubscribeActorEventsTransport
    {
        public Channel<ChannelStream> Opened { get; } = Channel.CreateUnbounded<ChannelStream>();
        public ValueTask<ISubscribeActorEventsStream> OpenStreamAsync(CancellationToken cancellationToken = default)
        {
            var stream = new ChannelStream(cancellationToken);
            Opened.Writer.TryWrite(stream);
            return ValueTask.FromResult<ISubscribeActorEventsStream>(stream);
        }
    }

    private sealed class ChannelStream(CancellationToken callCancellation) : ISubscribeActorEventsStream
    {
        public Channel<SubscribeActorEventsRequest> Requests { get; } = Channel.CreateUnbounded<SubscribeActorEventsRequest>();
        public Channel<SubscribeActorEventsResponse> Responses { get; } = Channel.CreateUnbounded<SubscribeActorEventsResponse>();
        public TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken CallCancellation => callCancellation;
        public Task? DisposalGate { get; set; }
        public Exception? DisposalError { get; set; }
        public int DisposeCount { get; private set; }

        public IAsyncEnumerable<SubscribeActorEventsRequest> ReadAllAsync(CancellationToken cancellationToken = default) =>
            Requests.Reader.ReadAllAsync(cancellationToken);
        public ValueTask WriteAsync(SubscribeActorEventsResponse response, CancellationToken cancellationToken = default) =>
            Responses.Writer.WriteAsync(response, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            DisposalStarted.TrySetResult();
            if (DisposalGate is not null)
            {
                await DisposalGate;
            }
            Requests.Writer.TryComplete();
            Responses.Writer.TryComplete();
            Disposed.TrySetResult();
            if (DisposalError is not null)
            {
                throw DisposalError;
            }
        }
    }

    private sealed class ReconnectClock : TimeProvider
    {
        public Channel<ManualTimer> Delays { get; } = Channel.CreateUnbounded<ManualTimer>();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            Delays.Writer.TryWrite(timer);
            return timer;
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        private int disposed;
        public void Fire()
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                callback(state);
            }
        }
        public bool Change(TimeSpan dueTime, TimeSpan period) => Volatile.Read(ref disposed) == 0;
        public void Dispose() => Interlocked.Exchange(ref disposed, 1);
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
