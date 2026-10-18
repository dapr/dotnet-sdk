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
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Common.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Testing.Test;

public sealed class VirtualTimerConcurrencyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly ActorId Id = ActorId.Create("virtual-timers");
    private static readonly IActorWireSerializer Serializer = new ActorWireSerializer(new JsonDaprSerializer());

    [Fact]
    public void Raw_timers_fire_in_due_time_then_registration_order_and_support_change()
    {
        var time = new VirtualActorTimeProvider();
        var observed = new List<string>();
        using var first = time.CreateTimer(_ => observed.Add($"a:{time.GetUtcNow().ToUnixTimeSeconds()}"),
            null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
        using var second = time.CreateTimer(_ => observed.Add($"b:{time.GetUtcNow().ToUnixTimeSeconds()}"),
            null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        Assert.True(first.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

        time.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(["a:1", "b:2", "a:3", "b:3", "b:4"], observed);
        Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(4), time.GetUtcNow());
        Assert.True(first.Change(System.Threading.Timeout.InfiniteTimeSpan, TimeSpan.FromSeconds(1)));
        second.Dispose();
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(5, observed.Count);
        Assert.False(second.Change(TimeSpan.Zero, TimeSpan.Zero));
    }

    [Fact]
    public async Task Virtual_timers_capture_and_suppress_execution_context_like_system_timers()
    {
        var local = new AsyncLocal<string?>();
        foreach (var provider in new[] { TimeProvider.System, new VirtualActorTimeProvider() })
        {
            var captured = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var suppressed = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            local.Value = "registration";
            using var first = provider.CreateTimer(_ =>
            {
                captured.TrySetResult(local.Value);
                local.Value = "callback-only";
            }, null, TimeSpan.FromMilliseconds(1), System.Threading.Timeout.InfiniteTimeSpan);
            ITimer second;
            using (ExecutionContext.SuppressFlow())
            {
                second = provider.CreateTimer(_ => suppressed.TrySetResult(local.Value),
                    null, TimeSpan.FromMilliseconds(1), System.Threading.Timeout.InfiniteTimeSpan);
            }

            using (second)
            {
                local.Value = "advancement";
                if (provider is VirtualActorTimeProvider virtualTime)
                {
                    virtualTime.Advance(TimeSpan.FromMilliseconds(1));
                }

                Assert.Equal("registration", await captured.Task.WaitAsync(Timeout));
                Assert.Null(await suppressed.Task.WaitAsync(Timeout));
                Assert.Equal("advancement", local.Value);
            }
        }
    }

    [Fact]
    public async Task Callbacks_run_outside_registration_lock_and_dispose_async_waits_for_admitted_callback()
    {
        var time = new VirtualActorTimeProvider();
        var entered = Signal();
        using var release = new ManualResetEventSlim();
        using var timer = time.CreateTimer(_ =>
        {
            entered.TrySetResult();
            if (!release.Wait(Timeout))
            {
                throw new TimeoutException("Callback was not released.");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        var advance = Task.Run(() => time.Advance(TimeSpan.FromSeconds(2)));
        Task? disposal = null;
        try
        {
            await entered.Task.WaitAsync(Timeout);
            await Task.Run(() =>
            {
                Assert.Equal(DateTimeOffset.UnixEpoch, time.GetUtcNow());
                Assert.True(timer.Change(TimeSpan.FromSeconds(1), TimeSpan.Zero));
            }).WaitAsync(Timeout);
            Assert.Throws<InvalidOperationException>(() => time.Advance(TimeSpan.Zero));
            disposal = timer.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
        }
        finally
        {
            release.Set();
            await advance.WaitAsync(Timeout);
            if (disposal is not null)
            {
                await disposal.WaitAsync(Timeout);
            }
        }

        Assert.False(timer.Change(TimeSpan.Zero, TimeSpan.Zero));
    }

    [Fact]
    public void Recursive_advancement_is_rejected_without_corrupting_the_clock()
    {
        var time = new VirtualActorTimeProvider();
        using var timer = time.CreateTimer(_ =>
            Assert.Throws<InvalidOperationException>(() => time.Advance(TimeSpan.FromSeconds(5))),
            null, TimeSpan.FromSeconds(1), TimeSpan.Zero);
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(2), time.GetUtcNow());
    }

    [Fact]
    public void Callback_failure_stops_at_due_time_and_allows_later_advancement()
    {
        var time = new VirtualActorTimeProvider();
        using var timer = time.CreateTimer(_ => throw new InvalidOperationException("failure"),
            null, TimeSpan.FromSeconds(1), TimeSpan.Zero);
        Assert.Throws<InvalidOperationException>(() => time.Advance(TimeSpan.FromSeconds(3)));
        Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(1), time.GetUtcNow());
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(2), time.GetUtcNow());
    }

    [Fact]
    public async Task Concurrent_changes_do_not_duplicate_callbacks_and_task_delay_uses_virtual_time()
    {
        var time = new VirtualActorTimeProvider();
        var count = 0;
        using var timer = time.CreateTimer(_ => count++, null, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        await RaceAsync(32, _ =>
        {
            Assert.True(timer.Change(TimeSpan.FromSeconds(1), TimeSpan.Zero));
            return Task.CompletedTask;
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.Change(TimeSpan.FromMilliseconds(-2), TimeSpan.Zero));
        var delay = Task.Delay(TimeSpan.FromSeconds(2), time);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, count);
        Assert.False(delay.IsCompleted);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(delay.IsCompletedSuccessfully);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Core_schedulers_admit_callbacks_at_each_virtual_due_time_before_advance_returns()
    {
        var time = new VirtualActorTimeProvider();
        var observed = new List<string>();
        var runtime = new RecordingRuntime(request =>
            observed.Add($"{request.OperationName}:{time.GetUtcNow().ToUnixTimeSeconds()}"));
        using var timers = new CoreActorTimerScheduler(runtime, Serializer, time);
        using var reminders = new CoreActorReminderScheduler(runtime, Serializer, time);
        Assert.True(timers.ScheduleAsync("A", Id, "timer", TimeSpan.FromSeconds(1), "Tick", "{}",
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)).IsCompletedSuccessfully);
        Assert.True(reminders.ScheduleAsync("A", Id, "Reminder", TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2), "{}").IsCompletedSuccessfully);

        time.Advance(TimeSpan.FromSeconds(7));

        Assert.Equal(["Tick:1", "Reminder:2", "Tick:3", "Reminder:4", "Reminder:6"], observed);
        Assert.True(reminders.CancelAsync("A", Id, "Reminder").IsCompletedSuccessfully);
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(5, observed.Count);
    }

    [Fact]
    public void Core_schedulers_suppress_registration_and_advance_context_and_callbacks_can_cancel_themselves()
    {
        var local = new AsyncLocal<string?> { Value = "registration" };
        var time = new VirtualActorTimeProvider();
        CoreActorTimerScheduler? timers = null;
        CoreActorReminderScheduler? reminders = null;
        var count = 0;
        var runtime = new RecordingRuntime(request =>
        {
            Assert.Null(local.Value);
            Assert.DoesNotContain(request.Headers, pair => pair.Value == "chain");
            count++;
            if (request.Kind == ActorTurnKind.Timer)
            {
                Assert.True(timers!.CancelAsync("A", Id, "Tick").IsCompletedSuccessfully);
            }
            else
            {
                Assert.True(reminders!.CancelAsync("A", Id, "Reminder").IsCompletedSuccessfully);
            }
        });
        using (timers = new CoreActorTimerScheduler(runtime, Serializer, time))
        using (reminders = new CoreActorReminderScheduler(runtime, Serializer, time))
        {
            Assert.True(timers.ScheduleAsync("A", Id, "Tick", TimeSpan.Zero, "Tick", "{}",
                TimeSpan.FromSeconds(1), headers: new Dictionary<string, string> { ["Dapr-Reentrancy-Id"] = "chain" }).IsCompletedSuccessfully);
            Assert.True(reminders.ScheduleAsync("A", Id, "Reminder", TimeSpan.Zero, TimeSpan.FromSeconds(1), "{}").IsCompletedSuccessfully);
            local.Value = "advance";
            time.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal(2, count);
            Assert.Equal("advance", local.Value);
        }
    }

    [Fact]
    public async Task Concurrent_core_replacements_and_cancel_leave_no_virtual_callbacks()
    {
        var time = new VirtualActorTimeProvider();
        var observed = new ConcurrentQueue<string>();
        var runtime = new RecordingRuntime(request => observed.Enqueue(request.OperationName));
        using var timers = new CoreActorTimerScheduler(runtime, Serializer, time);
        using var reminders = new CoreActorReminderScheduler(runtime, Serializer, time);
        await RaceAsync(32, async _ =>
        {
            await timers.ScheduleAsync("A", Id, "timer", TimeSpan.Zero, "Tick", "{}");
            await reminders.ScheduleAsync("A", Id, "Reminder", TimeSpan.Zero, TimeSpan.Zero, "{}");
        });
        await timers.CancelAsync("A", Id, "timer");
        await reminders.CancelAllAsync("A");
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(observed);
    }

    [Fact]
    public async Task Legacy_direct_helpers_replace_same_named_callbacks_without_losing_distinct_kinds()
    {
        var actor = new RecordingActor();
        await using var runtime = CreateRuntime(actor);
        await RaceAsync(32, index =>
        {
            runtime.Time.ScheduleTimer("A", Id, "Callback", TimeSpan.Zero, $"\"timer-{index}\"");
            runtime.Time.ScheduleReminder("A", Id, "Callback", TimeSpan.Zero, $"\"reminder-{index}\"");
            return Task.CompletedTask;
        });
        Assert.Single(await ((IActorReminderScheduler)runtime.Time).ListAsync("A"));

        runtime.Time.Advance(TimeSpan.Zero);
        await runtime.RunToIdle().WaitAsync(Timeout);

        Assert.Equal(2, actor.Payloads.Count);
        Assert.Single(actor.Payloads, value => value.Contains("timer-", StringComparison.Ordinal));
        Assert.Single(actor.Payloads, value => value.Contains("reminder-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Legacy_reminder_overwrite_false_is_atomic_and_cancellation_removes_the_winner()
    {
        var actor = new RecordingActor();
        await using var runtime = CreateRuntime(actor);
        var reminders = (IActorReminderScheduler)runtime.Time;
        var accepted = 0;
        var rejected = 0;
        await RaceAsync(32, async _ =>
        {
            try
            {
                await reminders.ScheduleAsync("A", Id, "Callback", TimeSpan.Zero, TimeSpan.Zero, "{}", overwrite: false);
                Interlocked.Increment(ref accepted);
            }
            catch (ActorReminderAlreadyExistsException)
            {
                Interlocked.Increment(ref rejected);
            }
        });
        Assert.Equal(1, accepted);
        Assert.Equal(31, rejected);
        Assert.Single(await reminders.ListAsync("A"));
        await reminders.CancelAllAsync("A");
        runtime.Time.Advance(TimeSpan.Zero);
        await runtime.RunToIdle().WaitAsync(Timeout);
        Assert.Empty(actor.Payloads);
    }

    [Fact]
    public async Task Legacy_invalid_or_cancelled_replacements_preserve_the_original_registration()
    {
        var actor = new RecordingActor();
        await using var runtime = CreateRuntime(actor);
        var timers = (IActorTimerScheduler)runtime.Time;
        var reminders = (IActorReminderScheduler)runtime.Time;
        await timers.ScheduleAsync("A", Id, "timer", TimeSpan.Zero, "Callback", "\"timer\"");
        await reminders.ScheduleAsync("A", Id, "Callback", TimeSpan.Zero, TimeSpan.Zero, "\"reminder\"");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            timers.RescheduleAsync("A", Id, "timer", TimeSpan.FromSeconds(-1), "Callback", "\"bad\"").AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            reminders.ScheduleAsync("A", Id, "Callback", TimeSpan.FromSeconds(-1), TimeSpan.Zero, "\"bad\"").AsTask());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timers.CancelAsync("A", Id, "timer", token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reminders.CancelAllAsync("A", cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            timers.ScheduleAsync("A", Id, "timer", TimeSpan.Zero, "Callback", 1, cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reminders.ScheduleAsync("A", Id, "Callback", TimeSpan.Zero, TimeSpan.Zero, new byte[0], cancellationToken: token).AsTask());
        runtime.Time.Advance(TimeSpan.Zero);
        await runtime.RunToIdle().WaitAsync(Timeout);
        Assert.Equal(["\"timer\"", "\"reminder\""], actor.Payloads.OrderByDescending(value => value).ToArray());
    }

    [Fact]
    public async Task Legacy_periodic_timers_and_reminders_expire_at_virtual_ttl()
    {
        var actor = new RecordingActor();
        await using var runtime = CreateRuntime(actor);
        await runtime.Time.ScheduleAsync("A", Id, "timer", TimeSpan.FromSeconds(1), "Callback", "\"timer\"",
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
        await ((IActorReminderScheduler)runtime.Time).ScheduleAsync("A", Id, "Callback", TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2), "\"reminder\"", TimeSpan.FromSeconds(4));
        runtime.Time.Advance(TimeSpan.FromSeconds(10));
        await runtime.RunToIdle().WaitAsync(Timeout);
        Assert.Equal(4, actor.Payloads.Count);
        Assert.Equal(2, actor.Payloads.Count(value => value == "\"timer\""));
        Assert.Equal(2, actor.Payloads.Count(value => value == "\"reminder\""));
    }

    [Fact]
    public async Task Advance_inside_actor_does_not_reenter_a_legacy_callback()
    {
        var actor = new RecordingActor();
        await using var runtime = CreateRuntime(actor);
        actor.AdvanceInsideTurn = headers =>
        {
            runtime.Time.ScheduleTimer("A", Id, "Callback", TimeSpan.Zero, headers: headers);
            runtime.Time.Advance(TimeSpan.Zero);
        };
        var call = runtime.InvokeAsync("A", Id, "AdvanceInside",
            headers: new Dictionary<string, string> { ["Dapr-Reentrancy-Id"] = "outer" });
        await runtime.RunToIdle().WaitAsync(Timeout);
        await call.WaitAsync(Timeout);
        Assert.False(actor.Overlapped);
        Assert.Equal(2, actor.Value);
    }

    [Fact]
    public async Task Core_timer_registered_and_advanced_inside_actor_is_admitted_without_reentrancy()
    {
        var actor = new RecordingActor();
        var time = new VirtualActorTimeProvider();
        var scheduler = new SeededRandomActorScheduler(0);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IActorScheduler>(scheduler);
        services.AddDaprActorsCore(registrations =>
            registrations.Add("A", typeof(IActor), typeof(RecordingActor), (_, _) => actor, new RecordingDispatcher()));
        services.AddSingleton<IActorStateStore, InMemoryActorStateStore>();
        await using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        using var timers = new CoreActorTimerScheduler(runtime, Serializer, time);
        actor.AdvanceInsideTurn = headers =>
        {
            Assert.True(timers.ScheduleAsync("A", Id, "timer", TimeSpan.Zero, "Callback", "{}", headers: headers).IsCompletedSuccessfully);
            time.Advance(TimeSpan.Zero);
        };

        var call = runtime.InvokeAsync("A", Id.Value, "AdvanceInside", ReadOnlyMemory<byte>.Empty,
            new Dictionary<string, string> { ["Dapr-Reentrancy-Id"] = "outer" });
        await scheduler.RunToIdleAsync().WaitAsync(Timeout);
        await call.WaitAsync(Timeout);

        Assert.False(actor.Overlapped);
        Assert.Equal(2, actor.Value);
        Assert.Single(actor.Payloads);
    }

    [Fact]
    public Task Harness_timer_registered_inside_held_actor_turn_does_not_lose_persisted_update() =>
        VerifyHeldHarnessCallbackAsync(reminder: false);

    [Fact]
    public Task Harness_reminder_registered_inside_held_actor_turn_does_not_lose_persisted_update() =>
        VerifyHeldHarnessCallbackAsync(reminder: true);

    private static async Task VerifyHeldHarnessCallbackAsync(bool reminder)
    {
        var gate = new HeldTurnGate();
        await using var runtime = new ActorTestRuntime(services =>
            services.AddDaprActorsCore(registrations => registrations.Add(
                "HeldCounter",
                typeof(IActor),
                typeof(HeldCounterActor),
                (provider, _) => new HeldCounterActor(
                    provider.GetRequiredService<ActorActivationContext>().State,
                    Assert.IsType<CoreActorTimerScheduler>(provider.GetRequiredService<IActorTimerScheduler>()),
                    Assert.IsType<CoreActorReminderScheduler>(provider.GetRequiredService<IActorReminderScheduler>())),
                new HeldCounterDispatcher(gate, reminder))));
        await runtime.SeedStateAsync("HeldCounter", Id, "counter", new PersistedCounter(), ActorStateSeedForm.Plain);
        var method = runtime.InvokeAsync("HeldCounter", Id, "RegisterAndHold",
            headers: new Dictionary<string, string> { ["Dapr-Reentrancy-Id"] = "registering-turn" });
        try
        {
            Assert.True(await runtime.StepAsync());
            await gate.Registered.Task.WaitAsync(Timeout);

            runtime.Time.Advance(TimeSpan.FromSeconds(1));

            Assert.False(await runtime.StepAsync());
            Assert.False(gate.CallbackEntered.Task.IsCompleted);
            Assert.False(method.IsCompleted);
            Assert.Equal(0, runtime.StateOf("HeldCounter", Id).Get<PersistedCounter>("counter")!.Value);
        }
        finally
        {
            gate.Release.TrySetResult();
            await runtime.RunToIdle().WaitAsync(Timeout);
            await method.WaitAsync(Timeout);
        }

        Assert.True(gate.CallbackEntered.Task.IsCompletedSuccessfully);
        Assert.False(gate.Overlapped);
        Assert.Equal(2, runtime.StateOf("HeldCounter", Id).Get<PersistedCounter>("counter")!.Value);
        Assert.Equal(["RegisterAndHold", "Increment"], runtime.Transcript.Select(turn => turn.OperationName).ToArray());
        Assert.Equal(reminder ? ActorTurnKind.Reminder : ActorTurnKind.Timer, runtime.Transcript[1].Kind);
    }

    private static ActorTestRuntime CreateRuntime(RecordingActor actor) => new(services =>
        services.AddDaprActorsCore(registrations =>
            registrations.Add("A", typeof(IActor), typeof(RecordingActor), (_, _) => actor, new RecordingDispatcher())));

    private static async Task RaceAsync(int count, Func<int, Task> operation)
    {
        var start = Signal();
        var tasks = Enumerable.Range(0, count).Select(async index =>
        {
            await start.Task;
            await operation(index);
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(tasks).WaitAsync(Timeout);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public sealed class PersistedCounter
    {
        public int Value { get; set; }
    }

    private sealed class HeldTurnGate
    {
        public TaskCompletionSource Registered { get; } = Signal();
        public TaskCompletionSource Release { get; } = Signal();
        public TaskCompletionSource CallbackEntered { get; } = Signal();
        public bool Holding;
        public bool Overlapped;
    }

    private sealed record HeldCounterActor(
        IActorStateAccessor State,
        IActorTimerScheduler Timers,
        IActorReminderScheduler Reminders) : IActor;

    private sealed class HeldCounterDispatcher(HeldTurnGate gate, bool reminder) : IActorDispatcher
    {
        public async ValueTask<ActorDispatchResponse> DispatchAsync(
            IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            var counter = (HeldCounterActor)actor;
            var state = await counter.State.GetOrCreateAsync("counter", () => new PersistedCounter(), cancellationToken);
            if (request.MethodName == "RegisterAndHold")
            {
                var previous = state.Value.Value;
                gate.Holding = true;
                try
                {
                    if (reminder)
                    {
                        await counter.Reminders.ScheduleAsync("HeldCounter", request.ActorId, "Increment",
                            TimeSpan.FromSeconds(1), TimeSpan.Zero, "{}", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await counter.Timers.ScheduleAsync("HeldCounter", request.ActorId, "timer",
                            TimeSpan.FromSeconds(1), "Increment", "{}", headers: request.Headers,
                            cancellationToken: cancellationToken);
                    }

                    gate.Registered.TrySetResult();
                    await gate.Release.Task.WaitAsync(cancellationToken);
                    state.Value.Value = previous + 1;
                }
                finally
                {
                    gate.Holding = false;
                }
            }
            else
            {
                gate.Overlapped |= gate.Holding;
                gate.CallbackEntered.TrySetResult();
                state.Value.Value++;
            }

            return new ActorDispatchResponse([]);
        }
    }

    private sealed class RecordingRuntime(Action<ActorRuntimeRequest> record) : IActorRuntime
    {
        public Task<byte[]?> DispatchAsync(ActorRuntimeRequest request, CancellationToken cancellationToken = default)
        {
            record(request);
            return Task.FromResult<byte[]?>(null);
        }

        public Task<byte[]?> InvokeAsync(string actorType, string actorId, string methodName, ReadOnlyMemory<byte> payload,
            IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeactivateAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingActor : IActor
    {
        public List<string> Payloads { get; } = [];
        public Action<IReadOnlyDictionary<string, string>>? AdvanceInsideTurn;
        public bool Holding;
        public bool Overlapped;
        public int Value;
    }

    private sealed class RecordingDispatcher : IActorDispatcher
    {
        public ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            var recording = (RecordingActor)actor;
            if (request.MethodName == "AdvanceInside")
            {
                recording.Holding = true;
                var previous = recording.Value;
                recording.AdvanceInsideTurn!(request.Headers);
                recording.Value = previous + 1;
                recording.Holding = false;
            }
            else
            {
                recording.Overlapped |= recording.Holding;
                recording.Value++;
                recording.Payloads.Add(System.Text.Encoding.UTF8.GetString(request.Payload.Span));
            }

            return ValueTask.FromResult(new ActorDispatchResponse([]));
        }
    }
}
