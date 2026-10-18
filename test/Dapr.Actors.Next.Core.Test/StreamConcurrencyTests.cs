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
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Core.Test;

public sealed class StreamConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_source_is_retained_until_async_callbacks_finish(bool cancelParent)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var parent = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        using var cancellation = new SubscribeActorEventsStreamManager.StreamCancellation(parent.Token);
        var token = cancellation.Token;
        using var registration = token.Register(() =>
        {
            entered.TrySetResult();
            release.Wait(timeout.Token);
            throw new InvalidOperationException("Cancellation callback failure");
        });
        Task callbacks;
        if (cancelParent)
        {
            parent.Cancel();
            callbacks = cancellation.CancelAsync();
        }
        else
        {
            callbacks = cancellation.CancelAsync();
        }

        try
        {
            await entered.Task.WaitAsync(timeout.Token);
            cancellation.Dispose();
            Assert.False(callbacks.IsCompleted);
            Assert.Equal(token, cancellation.Token);
            Assert.Same(callbacks, cancellation.CancelAsync());
        }
        finally
        {
            release.Set();
        }

        var error = await Assert.ThrowsAsync<AggregateException>(() => callbacks.WaitAsync(timeout.Token));
        Assert.Equal("Cancellation callback failure", Assert.Single(error.InnerExceptions).Message);
    }

    [Fact]
    public async Task Acyclic_callback_chain_exceeding_worker_limit_completes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = new ChannelTransport();
        ChannelStream? stream = null;
        var dispatcher = new CallbackDispatcher(async (request, token) =>
        {
            var depth = int.Parse(request.ActorId.Value);
            if (depth < 80)
            {
                var child = await stream!.InvokeAsync((depth + 1).ToString(), "Nested", token, request.Headers);
                AssertSuccessful(child);
            }

            return new ActorDispatchResponse("1"u8.ToArray());
        });
        await using var provider = CreateProvider(dispatcher);
        using var manager = CreateManager(provider, transport);
        try
        {
            await manager.StartAsync(timeout.Token);
            stream = await transport.Opened.Reader.ReadAsync(timeout.Token);
            await stream.Advertised.Task.WaitAsync(timeout.Token);

            AssertSuccessful(await stream.InvokeAsync("0", "Nested", timeout.Token));
            Assert.Equal(81, dispatcher.Completed);
            Assert.Equal(1, stream.MaxConcurrentWrites);
        }
        finally
        {
            await manager.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task Queued_normal_turns_do_not_hide_nested_callback_behind_backpressure()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = new ChannelTransport();
        var entered = Signal();
        var beginNested = Signal();
        ChannelStream? stream = null;
        var dispatcher = new CallbackDispatcher(async (request, token) =>
        {
            if (request.MethodName == "Outer")
            {
                entered.TrySetResult();
                await beginNested.Task.WaitAsync(token);
                AssertSuccessful(await stream!.InvokeAsync("child", "Child", token, request.Headers));
            }

            return new ActorDispatchResponse("1"u8.ToArray());
        });
        await using var provider = CreateProvider(dispatcher);
        using var manager = CreateManager(provider, transport);
        try
        {
            await manager.StartAsync(timeout.Token);
            stream = await transport.Opened.Reader.ReadAsync(timeout.Token);
            await stream.Advertised.Task.WaitAsync(timeout.Token);
            var outer = stream.InvokeAsync("parent", "Outer", timeout.Token);
            await entered.Task.WaitAsync(timeout.Token);
            var queued = Enumerable.Range(0, 80)
                .Select(_ => stream.InvokeAsync("parent", "Queued", timeout.Token)).ToArray();
            for (var i = 0; i < 81; i++)
            {
                await stream.ReadFrames.Reader.ReadAsync(timeout.Token);
            }

            beginNested.TrySetResult();
            AssertSuccessful(await outer);
            foreach (var response in await Task.WhenAll(queued))
            {
                AssertSuccessful(response);
            }
        }
        finally
        {
            await manager.StopAsync(timeout.Token);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Failed_io_cancels_siblings_disposes_and_reconnects(bool failWriter)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = new ChannelTransport();
        var clock = new ReconnectClock();
        var entered = Signal();
        var cancelled = Signal();
        var dispatcher = new CallbackDispatcher(async (request, token) =>
        {
            if (request.MethodName == "Hold")
            {
                entered.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    cancelled.TrySetResult();
                }
            }

            return new ActorDispatchResponse("1"u8.ToArray());
        });
        await using var provider = CreateProvider(dispatcher);
        using var manager = CreateManager(provider, transport, clock);
        try
        {
            await manager.StartAsync(timeout.Token);
            var first = await transport.Opened.Reader.ReadAsync(timeout.Token);
            await first.Advertised.Task.WaitAsync(timeout.Token);
            await first.SendAsync("blocked", "Hold", timeout.Token);
            await entered.Task.WaitAsync(timeout.Token);

            if (failWriter)
            {
                first.FailWrites = true;
                await first.SendAsync("other", "Complete", timeout.Token);
            }
            else
            {
                first.FailReads();
            }

            await first.Disposed.Task.WaitAsync(timeout.Token);
            await first.ReadStopped.Task.WaitAsync(timeout.Token);
            await cancelled.Task.WaitAsync(timeout.Token);
            Assert.True(first.CallCancellation.IsCancellationRequested);
            (await clock.Delays.Reader.ReadAsync(timeout.Token)).Fire();
            var second = await transport.Opened.Reader.ReadAsync(timeout.Token);
            await second.Advertised.Task.WaitAsync(timeout.Token);
            AssertSuccessful(await second.InvokeAsync("healthy", "Complete", timeout.Token));
        }
        finally
        {
            await manager.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task Graceful_eof_drains_accepted_turn_before_disposing()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = new ChannelTransport();
        var entered = Signal();
        var release = Signal();
        var dispatcher = new CallbackDispatcher(async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return new ActorDispatchResponse("1"u8.ToArray());
        });
        await using var provider = CreateProvider(dispatcher);
        using var manager = CreateManager(provider, transport);
        try
        {
            await manager.StartAsync(timeout.Token);
            var first = await transport.Opened.Reader.ReadAsync(timeout.Token);
            await first.Advertised.Task.WaitAsync(timeout.Token);
            var call = first.InvokeAsync("one", "Hold", timeout.Token);
            await entered.Task.WaitAsync(timeout.Token);
            first.CompleteReads();
            await first.ReadStopped.Task.WaitAsync(timeout.Token);
            Assert.False(first.Disposed.Task.IsCompleted);
            Assert.False(call.IsCompleted);
            release.TrySetResult();
            AssertSuccessful(await call);
            await first.Disposed.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            await manager.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task Callback_response_is_not_written_until_state_commit_completes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = new ChannelTransport();
        var store = new GatedStateStore();
        await using var provider = CreateProvider(new CommitDispatcher(), store);
        using var manager = CreateManager(provider, transport);
        try
        {
            await manager.StartAsync(timeout.Token);
            var stream = await transport.Opened.Reader.ReadAsync(timeout.Token);
            await stream.Advertised.Task.WaitAsync(timeout.Token);
            var call = stream.InvokeAsync("one", "Update", timeout.Token);
            await store.Writing.Task.WaitAsync(timeout.Token);
            Assert.False(stream.CallbackWriteStarted.Task.IsCompleted);
            Assert.False(call.IsCompleted);
            store.Release.TrySetResult();
            AssertSuccessful(await call);
            Assert.True(store.Committed);
        }
        finally
        {
            store.Release.TrySetResult();
            await manager.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task Shutdown_cancels_blocked_writer_and_reader()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = new ChannelTransport();
        var writeGate = Signal();
        var dispatcher = new CallbackDispatcher((_, _) => Task.FromResult(new ActorDispatchResponse("1"u8.ToArray())));
        await using var provider = CreateProvider(dispatcher);
        using var manager = CreateManager(provider, transport);
        try
        {
            await manager.StartAsync(timeout.Token);
            var stream = await transport.Opened.Reader.ReadAsync(timeout.Token);
            await stream.Advertised.Task.WaitAsync(timeout.Token);
            stream.WriteGate = writeGate.Task;
            await stream.SendAsync("one", "Update", timeout.Token);
            await stream.CallbackWriteStarted.Task.WaitAsync(timeout.Token);
            await manager.StopAsync(timeout.Token);
            await stream.CallbackWriteStopped.Task.WaitAsync(timeout.Token);
            await stream.ReadStopped.Task.WaitAsync(timeout.Token);
            await stream.Disposed.Task.WaitAsync(timeout.Token);
            Assert.True(stream.CallCancellation.IsCancellationRequested);
            Assert.False(writeGate.Task.IsCompleted);
        }
        finally
        {
            writeGate.TrySetResult();
            await manager.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task Shutdown_is_bounded_when_actor_and_disposal_ignore_cancellation()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = new ChannelTransport();
        var entered = Signal();
        var releaseActor = Signal();
        var actorExited = Signal();
        var releaseDisposal = Signal();
        var dispatcher = new CallbackDispatcher(async (_, _) =>
        {
            entered.TrySetResult();
            await releaseActor.Task;
            actorExited.TrySetResult();
            throw new InvalidOperationException("Late actor failure");
        });
        await using var provider = CreateProvider(dispatcher);
        using var manager = CreateManager(provider, transport);
        ChannelStream? stream = null;
        try
        {
            await manager.StartAsync(timeout.Token);
            stream = await transport.Opened.Reader.ReadAsync(timeout.Token);
            stream.DisposalGate = releaseDisposal.Task;
            await stream.Advertised.Task.WaitAsync(timeout.Token);
            await stream.SendAsync("one", "Hold", timeout.Token);
            await entered.Task.WaitAsync(timeout.Token);
            await manager.StopAsync(timeout.Token).WaitAsync(timeout.Token);
            await stream.DisposalStarted.Task.WaitAsync(timeout.Token);
            await stream.ReadStopped.Task.WaitAsync(timeout.Token);
            Assert.False(actorExited.Task.IsCompleted);
            Assert.False(stream.Disposed.Task.IsCompleted);
        }
        finally
        {
            releaseActor.TrySetResult();
            releaseDisposal.TrySetResult();
            await actorExited.Task.WaitAsync(timeout.Token);
            if (stream is not null)
            {
                await stream.Disposed.Task.WaitAsync(timeout.Token);
            }
            await manager.StopAsync(timeout.Token);
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void AssertSuccessful(SubscribeActorEventsResponse response)
    {
        Assert.False(response.Error);
        Assert.Null(response.FailureMessage);
    }

    private static ServiceProvider CreateProvider(IActorDispatcher dispatcher, IActorStateStore? store = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryActorAdapters();
        if (store is not null)
        {
            services.AddSingleton(store);
        }
        services.AddDaprActorsCore(registrations => registrations.Add(
            "StreamActor", typeof(IStreamActor), typeof(StreamActor),
            (sp, _) => new StreamActor(sp.GetRequiredService<ActorActivationContext>()), dispatcher));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static SubscribeActorEventsStreamManager CreateManager(
        IServiceProvider provider, ISubscribeActorEventsTransport transport, TimeProvider? clock = null) =>
        new(transport, provider.GetRequiredService<ActorRuntimeRegistry>(), provider.GetRequiredService<IActorRuntime>(),
            clock ?? TimeProvider.System, Options.Create(new DaprActorsOptions()),
            NullLogger<SubscribeActorEventsStreamManager>.Instance);

    private interface IStreamActor : IActor;
    private sealed class StreamActor(ActorActivationContext context) : IStreamActor
    {
        public IActorStateAccessor State => context.State;
    }

    private sealed class CommitDispatcher : IActorDispatcher
    {
        public async ValueTask<ActorDispatchResponse> DispatchAsync(
            IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            var state = await ((StreamActor)actor).State.GetOrCreateAsync(
                "counter", () => new CounterState(), cancellationToken);
            state.Value.Value++;
            return new ActorDispatchResponse("1"u8.ToArray());
        }
    }

    private sealed class GatedStateStore : IActorStateStore
    {
        public TaskCompletionSource Writing { get; } = Signal();
        public TaskCompletionSource Release { get; } = Signal();
        public bool Committed { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(
            string actorType, string actorId, string name, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);

        public async ValueTask WriteAsync(
            string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            Writing.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            Committed = true;
        }

        public ValueTask DeleteAsync(
            string actorType, string actorId, string name, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class CallbackDispatcher(
        Func<ActorDispatchRequest, CancellationToken, Task<ActorDispatchResponse>> dispatch) : IActorDispatcher
    {
        private int completed;
        public int Completed => Volatile.Read(ref completed);

        public async ValueTask<ActorDispatchResponse> DispatchAsync(
            IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            var response = await dispatch(request, cancellationToken);
            Interlocked.Increment(ref completed);
            return response;
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
        private readonly Channel<SubscribeActorEventsRequest> requests = Channel.CreateUnbounded<SubscribeActorEventsRequest>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<SubscribeActorEventsResponse>> pending = new();
        private int activeWrites;
        private int maxConcurrentWrites;

        public Channel<bool> ReadFrames { get; } = Channel.CreateUnbounded<bool>();
        public TaskCompletionSource Advertised { get; } = Signal();
        public TaskCompletionSource CallbackWriteStarted { get; } = Signal();
        public TaskCompletionSource CallbackWriteStopped { get; } = Signal();
        public TaskCompletionSource ReadStopped { get; } = Signal();
        public TaskCompletionSource DisposalStarted { get; } = Signal();
        public TaskCompletionSource Disposed { get; } = Signal();
        public CancellationToken CallCancellation => callCancellation;
        public bool FailWrites { get; set; }
        public Task? WriteGate { get; set; }
        public Task? DisposalGate { get; set; }
        public int MaxConcurrentWrites => Volatile.Read(ref maxConcurrentWrites);

        public async IAsyncEnumerable<SubscribeActorEventsRequest> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                await foreach (var request in requests.Reader.ReadAllAsync(cancellationToken))
                {
                    ReadFrames.Writer.TryWrite(true);
                    yield return request;
                }
            }
            finally
            {
                ReadStopped.TrySetResult();
            }
        }

        public async ValueTask WriteAsync(SubscribeActorEventsResponse response, CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref activeWrites);
            UpdateMaxConcurrentWrites(active);
            try
            {
                await Task.Yield();
                if (response.Kind != SubscribeActorEventsFrameKind.RegisteredActors)
                {
                    CallbackWriteStarted.TrySetResult();
                    if (WriteGate is not null)
                    {
                        await WriteGate.WaitAsync(cancellationToken);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (FailWrites)
                {
                    throw new IOException("Injected writer failure while input is open");
                }

                if (response.Kind == SubscribeActorEventsFrameKind.RegisteredActors)
                {
                    Advertised.TrySetResult();
                }
                else if (pending.TryRemove(response.Id, out var completion))
                {
                    completion.TrySetResult(response);
                }
            }
            finally
            {
                Interlocked.Decrement(ref activeWrites);
                if (response.Kind != SubscribeActorEventsFrameKind.RegisteredActors)
                {
                    CallbackWriteStopped.TrySetResult();
                }
            }
        }

        private void UpdateMaxConcurrentWrites(int value)
        {
            int previous;
            do
            {
                previous = Volatile.Read(ref maxConcurrentWrites);
            } while (value > previous && Interlocked.CompareExchange(ref maxConcurrentWrites, value, previous) != previous);
        }

        public async Task<SubscribeActorEventsResponse> InvokeAsync(
            string actorId, string method, CancellationToken token, IReadOnlyDictionary<string, string>? headers = null)
        {
            var id = Guid.NewGuid().ToString();
            var completion = new TaskCompletionSource<SubscribeActorEventsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = completion;
            try
            {
                await requests.Writer.WriteAsync(Request(id, actorId, method, headers), token);
                return await completion.Task.WaitAsync(token);
            }
            finally
            {
                pending.TryRemove(id, out _);
            }
        }

        public ValueTask SendAsync(string actorId, string method, CancellationToken token) =>
            requests.Writer.WriteAsync(Request(Guid.NewGuid().ToString(), actorId, method, null), token);

        private static SubscribeActorEventsRequest Request(
            string id, string actorId, string method, IReadOnlyDictionary<string, string>? headers) =>
            new(id, SubscribeActorEventsFrameKind.Invoke, "StreamActor", actorId, method, ReadOnlyMemory<byte>.Empty,
                headers ?? new Dictionary<string, string> { ["Dapr-Reentrancy-Id"] = Guid.NewGuid().ToString() });

        public void CompleteReads() => requests.Writer.TryComplete();
        public void FailReads() => requests.Writer.TryComplete(new IOException("Injected reader failure"));

        public async ValueTask DisposeAsync()
        {
            DisposalStarted.TrySetResult();
            if (DisposalGate is not null)
            {
                await DisposalGate;
            }
            requests.Writer.TryComplete();
            Disposed.TrySetResult();
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
