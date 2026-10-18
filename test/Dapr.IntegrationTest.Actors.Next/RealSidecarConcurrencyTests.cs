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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Core;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Core.Transport;
using Dapr.Actors.Next.Testing;
using Dapr.Testcontainers.Xunit.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.IntegrationTest.Actors.Next;

public sealed partial class RealSidecarActorTests
{
    private static partial void ConfigureConcurrencyActors(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ConcurrencyProbe>();
        builder.Services.AddSingleton<ActorFaults>();
        builder.Services.AddSingleton<IActorStateFaultInjector>(sp => sp.GetRequiredService<ActorFaults>());
        builder.Services.AddSingleton<ISubscribeActorEventsTransport>(sp =>
            new FaultInjectingActorEventsTransport(
                new ConcurrencyRecordingTransport(
                    new RecordingActorEventsTransport(sp.GetRequiredService<P.Dapr.DaprClient>()),
                    sp.GetRequiredService<ConcurrencyProbe>()),
                sp.GetRequiredService<ActorFaults>()));
        builder.Services.AddDaprActorsCore(registrations => registrations.Add(
            "ConcurrencyActor", typeof(IActor), typeof(ConcurrencyActor),
            static (sp, _) =>
            {
                var probe = sp.GetRequiredService<ConcurrencyProbe>();
                var actor = new ConcurrencyActor(
                    sp.GetRequiredService<ActorActivationContext>(),
                    sp.GetRequiredService<IActorInvocationClient>(),
                    sp.GetRequiredService<IActorRuntime>(),
                    sp.GetRequiredService<IActorTimerScheduler>(),
                    sp.GetRequiredService<IActorReminderScheduler>(), probe);
                probe.Actors.Add(actor);
                return actor;
            },
            new ConcurrencyDispatcher()));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Sidecar_reentrancy_propagates_canonical_header_and_completes_self_call()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await InvokeConcurrencyAsync("self", "Self", timeout.Token);
        var probe = app!.Services.GetRequiredService<ConcurrencyProbe>();
        var outer = Assert.Single(probe.Frames, frame => frame.MethodName == "Self");
        var inner = Assert.Single(probe.Frames, frame => frame.ActorId == "self" && frame.MethodName == "Leaf");
        Assert.False(string.IsNullOrWhiteSpace(ReentrancyId(outer.Headers)));
        Assert.Equal(ReentrancyId(outer.Headers), ReentrancyId(inner.Headers));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Sidecar_reentrancy_completes_A_B_A_call_chain()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await InvokeConcurrencyAsync("chain-a", "ChainStart", timeout.Token);
        var frames = app!.Services.GetRequiredService<ConcurrencyProbe>().Frames
            .Where(frame => frame.ActorType == "ConcurrencyActor").ToArray();
        Assert.Contains(frames, frame => frame.ActorId == "chain-b" && frame.MethodName == "ChainRelay");
        Assert.Contains(frames, frame => frame.ActorId == "chain-a" && frame.MethodName == "Leaf");
        Assert.Single(frames.Select(frame => ReentrancyId(frame.Headers)).Distinct());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Sidecar_nested_calls_exceed_former_worker_limit_without_deadlock()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var probe = app!.Services.GetRequiredService<ConcurrencyProbe>();
        var calls = Enumerable.Range(0, ConcurrencyProbe.ParallelCalls)
            .Select(index => InvokeConcurrencyAsync($"outer-{index}", "Fanout", timeout.Token))
            .ToArray();
        try
        {
            await probe.AllEntered.Task.WaitAsync(timeout.Token);
            probe.Release.TrySetResult();
            await Task.WhenAll(calls).WaitAsync(timeout.Token);
        }
        finally
        {
            probe.Release.TrySetResult();
        }

        Assert.Equal(ConcurrencyProbe.ParallelCalls, probe.Frames.Count(frame =>
            frame.MethodName == "Leaf" && frame.ActorId.StartsWith("leaf-", StringComparison.Ordinal)));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Sidecar_state_commit_finishes_before_external_deactivation()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var probe = app!.Services.GetRequiredService<ConcurrencyProbe>();
        using var pause = app.Services.GetRequiredService<ActorFaults>().PauseNextStateWrite<int>("count");
        var call = InvokeConcurrencyAsync("deactivate", "Increment", timeout.Token);
        await pause.Reached.WaitAsync(timeout.Token);
        var actor = Assert.Single(probe.Actors);
        var deactivate = app.Services.GetRequiredService<IActorRuntime>()
            .DeactivateAsync("ConcurrencyActor", ActorId.Create("deactivate"), timeout.Token);
        try
        {
            Assert.Equal(0, actor.DisposeCount);
            Assert.False(deactivate.IsCompleted);
        }
        finally
        {
            pause.Release();
        }

        await Task.WhenAll(call, deactivate).WaitAsync(timeout.Token);
        Assert.Equal(1, actor.DisposeCount);
        Assert.Equal(1, (await InvokeConcurrencyAsync("deactivate", "Read", timeout.Token)).Value);
        Assert.Equal(2, probe.Actors.Count);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Sidecar_self_deactivation_is_deferred_until_state_is_saved()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await InvokeConcurrencyAsync("self-deactivate", "SelfDeactivate", timeout.Token);
        Assert.False(result.DisposedDuringMethod);
        var old = Assert.Single(app!.Services.GetRequiredService<ConcurrencyProbe>().Actors);
        Assert.Equal(1, old.DisposeCount);
        Assert.Equal(1, (await InvokeConcurrencyAsync("self-deactivate", "Read", timeout.Token)).Value);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Sidecar_timer_does_not_overlap_its_registering_turn() => CheckSidecarCallbackAsync(reminder: false);

    [MinimumDaprRuntimeFact("1.18")]
    public Task Sidecar_reminder_does_not_overlap_its_registering_turn() => CheckSidecarCallbackAsync(reminder: true);

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Sidecar_concurrent_timer_and_reminder_replacements_can_be_cancelled()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var actorId = ActorId.Create("replace");
        await InvokeConcurrencyAsync(actorId.Value, "Read", timeout.Token);
        var timers = app!.Services.GetRequiredService<IActorTimerScheduler>();
        var reminders = app.Services.GetRequiredService<IActorReminderScheduler>();
        var probe = app.Services.GetRequiredService<ConcurrencyProbe>();
        var due = TimeSpan.FromSeconds(5);
        var period = TimeSpan.FromMilliseconds(100);
        await Task.WhenAll(Enumerable.Range(0, 8).SelectMany(_ => new[]
        {
            timers.ScheduleAsync("ConcurrencyActor", actorId, "callback", due, "Callback",
                Array.Empty<byte>(), period, cancellationToken: timeout.Token).AsTask(),
            reminders.ScheduleAsync("ConcurrencyActor", actorId, "Callback", due, period,
                Array.Empty<byte>(), overwrite: true, cancellationToken: timeout.Token).AsTask(),
        }));
        await timers.CancelAsync("ConcurrencyActor", actorId, "callback", timeout.Token);
        await reminders.CancelAsync("ConcurrencyActor", actorId, "Callback", timeout.Token);
        Assert.Null(await reminders.GetAsync("ConcurrencyActor", actorId, "Callback", timeout.Token));

        // Allow already-admitted callbacks to finish, then wait past every registration's first due time.
        await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
        var completed = Volatile.Read(ref probe.Callbacks);
        await Task.Delay(due + period, timeout.Token);
        Assert.Equal(completed, Volatile.Read(ref probe.Callbacks));
    }

    private async Task CheckSidecarCallbackAsync(bool reminder)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var probe = app!.Services.GetRequiredService<ConcurrencyProbe>();
        var call = InvokeConcurrencyAsync("callback", reminder ? "HoldWithReminder" : "HoldWithTimer", timeout.Token);
        await probe.AllEntered.Task.WaitAsync(timeout.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token);
            Assert.False(probe.Callback.Task.IsCompleted);
        }
        finally
        {
            probe.Release.TrySetResult();
        }

        await call.WaitAsync(timeout.Token);
        await probe.Callback.Task.WaitAsync(timeout.Token);
        Assert.False(probe.CallbackOverlapped);
        Assert.Equal(2, (await InvokeConcurrencyAsync("callback", "Read", timeout.Token)).Value);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Sidecar_reconnects_after_injected_response_loss() => CheckSidecarStreamFailureAsync(write: true);

    [MinimumDaprRuntimeFact("1.18")]
    public Task Sidecar_reconnects_after_injected_read_failure() => CheckSidecarStreamFailureAsync(write: false);

    private async Task CheckSidecarStreamFailureAsync(bool write)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var probe = app!.Services.GetRequiredService<ConcurrencyProbe>();
        var faults = app.Services.GetRequiredService<ActorFaults>();
        await InvokeConcurrencyAsync("fault", "Read", timeout.Token);
        var opens = Volatile.Read(ref probe.StreamOpens);
        if (write)
        {
            faults.FailNextStreamWrite(SubscribeActorEventsFrameKind.Invoke);
        }
        else
        {
            faults.FailNextStreamRead(SubscribeActorEventsFrameKind.Invoke);
        }

        using var interrupted = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var call = InvokeConcurrencyAsync("fault", "SetOnce", interrupted.Token);
        await WaitUntilAsync(() => Volatile.Read(ref probe.StreamOpens) > opens, timeout.Token,
            () => "The failed stream did not reconnect.");
        try
        {
            await call;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Cancelled or StatusCode.Unavailable or StatusCode.Internal or StatusCode.Unknown)
        {
            TestContext.Current.TestOutputHelper!.WriteLine($"Interrupted call: {ex.StatusCode}");
        }

        if (write)
        {
            Assert.Equal(1, (await InvokeConcurrencyAsync("fault", "Read", timeout.Token)).Value);
        }

        // Retry an idempotent operation: a lost response may follow a successful durable commit.
        await InvokeConcurrencyAsync("fault", "SetOnce", timeout.Token);
        Assert.Equal(1, (await InvokeConcurrencyAsync("fault", "Read", timeout.Token)).Value);
    }

    private async Task<ConcurrencyResult> InvokeConcurrencyAsync(string id, string method, CancellationToken ct)
    {
        var response = await client!.InvokeActorAsync(
            CreateInvoke("ConcurrencyActor", id, method, ReadOnlyMemory<byte>.Empty), cancellationToken: ct);
        return JsonSerializer.Deserialize<ConcurrencyResult>(response.Data.Span)!;
    }

    private static string? ReentrancyId(IReadOnlyDictionary<string, string> headers) =>
        headers.FirstOrDefault(pair => string.Equals(pair.Key, "Dapr-Reentrancy-Id", StringComparison.OrdinalIgnoreCase)).Value;

    private sealed record ConcurrencyResult(int Value = 0, bool DisposedDuringMethod = false);

    private sealed class ConcurrencyProbe
    {
        public const int ParallelCalls = 16;
        public ConcurrentQueue<SubscribeActorEventsRequest> Frames { get; } = new();
        public ConcurrentBag<ConcurrencyActor> Actors { get; } = new();
        public TaskCompletionSource AllEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Callback { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Entered;
        public int StreamOpens;
        public int Callbacks;
        public bool CallbackOverlapped;
    }

    private sealed class ConcurrencyActor(
        ActorActivationContext context, IActorInvocationClient invocation, IActorRuntime runtime,
        IActorTimerScheduler timers, IActorReminderScheduler reminders, ConcurrencyProbe probe) : IActor, IDisposable
    {
        public int DisposeCount;
        private bool holding;

        public async Task<ConcurrencyResult> InvokeAsync(ActorDispatchRequest request, CancellationToken ct)
        {
            var state = await context.State.GetOrCreateAsync("count", () => 0, ct);
            switch (request.MethodName)
            {
                case "Read":
                case "Leaf":
                    return new(state.Value);
                case "Increment":
                    state.Value++;
                    return new(state.Value);
                case "SetOnce":
                    state.Value = 1;
                    return new(state.Value);
                case "SelfDeactivate":
                    state.Value++;
                    await runtime.DeactivateAsync("ConcurrencyActor", context.ActorId, ct);
                    return new(state.Value, DisposeCount != 0);
                case "HoldWithTimer":
                case "HoldWithReminder":
                    holding = true;
                    var previous = state.Value;
                    if (request.MethodName == "HoldWithTimer")
                    {
                        await timers.ScheduleAsync("ConcurrencyActor", context.ActorId, "callback",
                            TimeSpan.FromMilliseconds(50), "Callback", Array.Empty<byte>(), cancellationToken: ct);
                    }
                    else
                    {
                        await reminders.ScheduleAsync("ConcurrencyActor", context.ActorId, "Callback",
                            TimeSpan.FromMilliseconds(50), TimeSpan.Zero, Array.Empty<byte>(), cancellationToken: ct);
                    }

                    probe.AllEntered.TrySetResult();
                    try
                    {
                        await probe.Release.Task.WaitAsync(ct);
                        state.Value = previous + 1;
                    }
                    finally
                    {
                        holding = false;
                    }

                    return new(state.Value);
                case "Callback":
                    Interlocked.Increment(ref probe.Callbacks);
                    probe.CallbackOverlapped = holding;
                    state.Value++;
                    probe.Callback.TrySetResult();
                    return new(state.Value);
            }

            var destination = request.ActorId.Value;
            var method = "Leaf";
            if (request.MethodName == "Fanout")
            {
                if (Interlocked.Increment(ref probe.Entered) == ConcurrencyProbe.ParallelCalls)
                {
                    probe.AllEntered.TrySetResult();
                }

                await probe.Release.Task.WaitAsync(ct);
                destination = $"leaf-{destination}";
            }
            else if (request.MethodName == "ChainStart")
            {
                destination = "chain-b";
                method = "ChainRelay";
            }
            else if (request.MethodName == "ChainRelay")
            {
                destination = "chain-a";
            }
            else if (request.MethodName != "Self")
            {
                throw new InvalidOperationException($"Unknown concurrency operation '{request.MethodName}'.");
            }

            using var innerTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            innerTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            await invocation.InvokeAsync("ConcurrencyActor", destination, method,
                ReadOnlyMemory<byte>.Empty, ActorHeaders.Empty, innerTimeout.Token);
            return new(state.Value);
        }

        public void Dispose() => Interlocked.Increment(ref DisposeCount);
    }

    private sealed class ConcurrencyDispatcher : IActorDispatcher
    {
        public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request,
            CancellationToken cancellationToken = default) =>
            new(JsonSerializer.SerializeToUtf8Bytes(
                await ((ConcurrencyActor)actor).InvokeAsync(request, cancellationToken)));
    }

    private sealed class ConcurrencyRecordingTransport(ISubscribeActorEventsTransport inner, ConcurrencyProbe probe)
        : ISubscribeActorEventsTransport
    {
        public async ValueTask<ISubscribeActorEventsStream> OpenStreamAsync(CancellationToken cancellationToken = default)
        {
            var stream = await inner.OpenStreamAsync(cancellationToken);
            Interlocked.Increment(ref probe.StreamOpens);
            return new RecordingStream(stream, probe);
        }

        private sealed class RecordingStream(ISubscribeActorEventsStream inner, ConcurrencyProbe probe)
            : ISubscribeActorEventsStream
        {
            public async IAsyncEnumerable<SubscribeActorEventsRequest> ReadAllAsync(
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var frame in inner.ReadAllAsync(cancellationToken))
                {
                    probe.Frames.Enqueue(frame);
                    yield return frame;
                }
            }

            public ValueTask WriteAsync(SubscribeActorEventsResponse response, CancellationToken cancellationToken = default) =>
                inner.WriteAsync(response, cancellationToken);

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
