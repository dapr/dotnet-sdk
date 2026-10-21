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
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Testing;
using Dapr.Common.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.Actors.Next.Core.Test;

public sealed class TimerConcurrencyTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly ActorId Id = ActorId.Create("timer-concurrency");
    private static readonly IActorWireSerializer Serializer = new ActorWireSerializer(new JsonDaprSerializer());

    [Theory]
    [InlineData(0, 1, 2, 2)]
    [InlineData(0, 0, 2, 1)]
    [InlineData(4, 1, 2, 0)]
    [InlineData(2, 1, 2, 0)]
    [InlineData(0, 1, 0, 0)]
    public async Task Reminder_ttl_expires_at_deadline_even_without_future_ticks(int due, int period, int ttl, int expected)
    {
        var time = new VirtualActorTimeProvider();
        var calls = new List<DateTimeOffset>();
        using var scheduler = new CoreActorReminderScheduler(new CallbackRuntime(_ =>
        {
            calls.Add(time.GetUtcNow());
            return Task.FromResult<byte[]?>(null);
        }), Serializer, time);
        await scheduler.ScheduleAsync("A", Id, "Tick", TimeSpan.FromSeconds(due), TimeSpan.FromSeconds(period), "{}",
            TimeSpan.FromSeconds(ttl));
        time.Advance(TimeSpan.FromSeconds(ttl));
        Assert.Equal(expected, calls.Count);
        Assert.All(calls, at => Assert.True(at < DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(ttl)));
        Assert.Null(await scheduler.GetAsync("A", Id, "Tick"));
        Assert.Empty(await scheduler.ListAsync("A"));
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(expected, calls.Count);
        await scheduler.ScheduleAsync("A", Id, "Tick", TimeSpan.FromSeconds(1), TimeSpan.Zero, "{}", overwrite: false);
        Assert.NotNull(await scheduler.GetAsync("A", Id, "Tick"));
    }

    [Fact]
    public async Task Core_timer_scheduler_gets_lists_and_drops_expired_timers()
    {
        var time = new ControlledTimeProvider();
        var scheduler = new CoreActorTimerScheduler(new CallbackRuntime(), Serializer, time);
        var otherId = ActorId.Create("other");
        await scheduler.ScheduleAsync("A", Id, "Tick", TimeSpan.FromSeconds(1), "OnTick", """{"x":1}""", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        await scheduler.ScheduleAsync("A", otherId, "Tick", TimeSpan.FromSeconds(1), "OnTick", "{}");

        var fetched = await scheduler.GetAsync("A", Id, "Tick");
        Assert.NotNull(fetched);
        Assert.Equal("A", fetched.ActorType);
        Assert.Equal(Id, fetched.ActorId);
        Assert.Equal(TimeSpan.FromSeconds(1), fetched.DueTime);
        Assert.Equal(TimeSpan.FromSeconds(1), fetched.Period);
        Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(2), fetched.ExpiresAt);
        Assert.Equal("OnTick", fetched.OperationName);
        Assert.Equal("""{"x":1}""", fetched.ArgumentsJson);
        Assert.Equal("Tick", Assert.Single(await scheduler.ListAsync("A", Id)).Name);
        Assert.Null(await scheduler.GetAsync("A", Id, "missing"));
        Assert.Empty(await scheduler.ListAsync("B", Id));

        time.Now += TimeSpan.FromSeconds(2);
        Assert.NotNull(await scheduler.GetAsync("A", Id, "Tick"));
        time.Now += TimeSpan.FromTicks(1);
        Assert.Null(await scheduler.GetAsync("A", Id, "Tick"));
        Assert.Empty(await scheduler.ListAsync("A", Id));
        Assert.Single(await scheduler.ListAsync("A", otherId));

        await scheduler.CancelAsync("A", otherId, "Tick");
        Assert.Empty(await scheduler.ListAsync("A", otherId));
        scheduler.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await scheduler.GetAsync("A", Id, "Tick"));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await scheduler.ListAsync("A", Id));
    }

    [Fact]
    public async Task Fired_one_shot_timer_without_ttl_is_no_longer_queryable()
    {
        var time = new VirtualActorTimeProvider();
        var calls = 0;
        using var scheduler = new CoreActorTimerScheduler(new CallbackRuntime(_ =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult<byte[]?>(null);
        }), Serializer, time);
        await scheduler.ScheduleAsync("A", Id, "Once", TimeSpan.FromSeconds(1), "OnTick", "{}");
        await scheduler.ScheduleAsync("A", Id, "Periodic", TimeSpan.FromSeconds(1), "OnTick", "{}", TimeSpan.FromSeconds(1));
        Assert.NotNull(await scheduler.GetAsync("A", Id, "Once"));
        Assert.Equal(2, (await scheduler.ListAsync("A", Id)).Count);

        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(2, calls);
        Assert.Null(await scheduler.GetAsync("A", Id, "Once"));
        Assert.Equal("Periodic", Assert.Single(await scheduler.ListAsync("A", Id)).Name);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, calls);
        Assert.Equal("Periodic", Assert.Single(await scheduler.ListAsync("A", Id)).Name);
    }

    [Fact]
    public async Task Completion_of_replaced_one_shot_timer_cannot_delete_its_replacement()
    {
        var time = new ControlledTimeProvider();
        using var scheduler = new CoreActorTimerScheduler(new CallbackRuntime(), Serializer, time);
        await scheduler.ScheduleAsync("A", Id, "Once", TimeSpan.Zero, "OnTick", "\"old\"");
        var oldTimers = time.Timers.ToArray();
        await scheduler.ScheduleAsync("A", Id, "Once", TimeSpan.Zero, "OnTick", "\"new\"");
        var newTimers = time.Timers.Except(oldTimers).ToArray();

        foreach (var timer in oldTimers) timer.Fire();
        Assert.Equal("\"new\"", (await scheduler.GetAsync("A", Id, "Once"))!.ArgumentsJson);

        foreach (var timer in newTimers) timer.Fire();
        Assert.All(newTimers, timer => Assert.True(timer.Disposed));
        Assert.Null(await scheduler.GetAsync("A", Id, "Once"));
        Assert.Empty(await scheduler.ListAsync("A", Id));
    }

    [Fact]
    public async Task Expiration_of_replaced_reminder_cannot_delete_its_replacement()
    {
        var time = new ControlledTimeProvider();
        using var scheduler = new CoreActorReminderScheduler(new CallbackRuntime(), Serializer, time);
        await scheduler.ScheduleAsync("A", Id, "Tick", TimeSpan.Zero, TimeSpan.Zero, "\"old\"", TimeSpan.FromSeconds(2));
        var oldTimers = time.Timers.ToArray();
        await scheduler.ScheduleAsync("A", Id, "Tick", TimeSpan.Zero, TimeSpan.Zero, "\"new\"", TimeSpan.FromSeconds(20));
        time.Now += TimeSpan.FromSeconds(3);
        foreach (var timer in oldTimers) timer.Fire();
        Assert.All(oldTimers, timer => Assert.True(timer.Disposed));
        Assert.Equal("\"new\"", (await scheduler.GetAsync("A", Id, "Tick"))!.ArgumentsJson);
        Assert.Single(await scheduler.ListAsync("A"));
    }

    [Fact]
    public async Task Reminder_expiration_does_not_interrupt_an_admitted_turn()
    {
        var time = new VirtualActorTimeProvider();
        var release = Signal();
        var finished = Signal();
        var calls = 0;
        using var scheduler = new CoreActorReminderScheduler(new CallbackRuntime(async _ =>
        {
            Interlocked.Increment(ref calls);
            await release.Task;
            finished.TrySetResult();
            return null;
        }), Serializer, time);
        await scheduler.ScheduleAsync("A", Id, "Tick", TimeSpan.Zero, TimeSpan.FromSeconds(1), "{}", TimeSpan.FromSeconds(1));
        try
        {
            time.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(1, calls);
            Assert.False(finished.Task.IsCompleted);
            Assert.Null(await scheduler.GetAsync("A", Id, "Tick"));
        }
        finally
        {
            release.TrySetResult();
        }
        await finished.Task.WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task Early_expiration_signal_is_rearmed_until_the_actual_deadline()
    {
        var time = new ControlledTimeProvider();
        using var scheduler = new CoreActorReminderScheduler(new CallbackRuntime(), Serializer, time);
        await scheduler.ScheduleAsync("A", Id, "Tick", TimeSpan.Zero, TimeSpan.Zero, "{}", TimeSpan.FromSeconds(2));
        var expiration = Assert.Single(time.Timers, timer => timer.DueTime == TimeSpan.FromSeconds(2));
        time.Now += TimeSpan.FromMilliseconds(1999.5);
        expiration.Fire();
        Assert.False(expiration.Disposed);
        Assert.Equal(TimeSpan.FromMilliseconds(1), expiration.DueTime);
        Assert.NotNull(await scheduler.GetAsync("A", Id, "Tick"));
        time.Now += TimeSpan.FromMilliseconds(1);
        expiration.Fire();
        Assert.All(time.Timers, timer => Assert.True(timer.Disposed));
        Assert.Null(await scheduler.GetAsync("A", Id, "Tick"));
    }

    [Fact]
    public Task System_timer_registered_inside_actor_is_an_independent_serialized_turn() =>
        VerifySystemCallbackAsync(reminder: false, forwardHeaders: false);

    [Fact]
    public Task System_timer_cannot_reuse_explicit_reentrancy_headers() =>
        VerifySystemCallbackAsync(reminder: false, forwardHeaders: true);

    [Fact]
    public Task System_reminder_registered_inside_actor_is_an_independent_serialized_turn() =>
        VerifySystemCallbackAsync(reminder: true, forwardHeaders: false);

    private static async Task VerifySystemCallbackAsync(bool reminder, bool forwardHeaders)
    {
        var actor = new GatedActor();
        var dispatcher = new GatedDispatcher(actor);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprActorsCore(registrations => registrations.Add(
            "TimerActor", typeof(IActor), typeof(GatedActor), (_, _) => actor, dispatcher));
        services.AddSingleton<IActorStateStore, InMemoryActorStateStore>();
        await using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var callbackAttempted = Signal();
        var callbackFinished = Signal();
        var observedRuntime = new CallbackRuntime(async request =>
        {
            var dispatch = runtime.DispatchAsync(request);
            callbackAttempted.TrySetResult();
            try
            {
                return await dispatch;
            }
            finally
            {
                callbackFinished.TrySetResult();
            }
        });
        using var timers = new CoreActorTimerScheduler(observedRuntime, Serializer, TimeProvider.System);
        using var reminders = new CoreActorReminderScheduler(observedRuntime, Serializer, TimeProvider.System);
        dispatcher.Schedule = headers => reminder
            ? reminders.ScheduleAsync("TimerActor", Id, "Callback", TimeSpan.Zero, TimeSpan.Zero, "{}")
            : timers.ScheduleAsync("TimerActor", Id, "timer", TimeSpan.Zero, "Callback", "{}",
                headers: forwardHeaders ? headers : null);
        var headers = new Dictionary<string, string>
        {
            ["DaPr-ReEnTrAnCy-Id"] = "outer-chain",
            ["DAPR-REENTRANT-ID"] = "outer-chain",
            ["X-DaPr-ReEnTrAnT-Id"] = "outer-chain",
            ["application-header"] = "preserved",
        };
        var method = runtime.InvokeAsync("TimerActor", Id.Value, "ScheduleAndHold", ReadOnlyMemory<byte>.Empty, headers);
        try
        {
            await callbackAttempted.Task.WaitAsync(TestTimeout);
            Assert.False(actor.CallbackEntered.Task.IsCompleted);
            Assert.False(method.IsCompleted);
        }
        finally
        {
            actor.Release.TrySetResult();
            await method.WaitAsync(TestTimeout);
            await callbackFinished.Task.WaitAsync(TestTimeout);
        }

        Assert.Equal(2, actor.Value);
        Assert.False(actor.Overlapped);
        if (forwardHeaders)
        {
            Assert.Equal("preserved", actor.CallbackHeaders!["application-header"]);
            Assert.DoesNotContain(actor.CallbackHeaders, pair => pair.Value == "outer-chain");
        }
    }

    [Fact]
    public async Task Concurrent_timer_replacements_leave_only_one_owned_timer_and_cancel_disposes_all()
    {
        var time = new ControlledTimeProvider();
        using var scheduler = new CoreActorTimerScheduler(new CallbackRuntime(), Serializer, time);
        await scheduler.ScheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", "{}");

        await RaceReplacementDuringDisposalAsync(Assert.Single(time.Timers),
            () => scheduler.ScheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", "{}").AsTask());
        await RaceAsync(32, _ => scheduler.RescheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", "{}").AsTask());

        Assert.Single(time.Timers, timer => !timer.Disposed);
        await scheduler.CancelAsync("A", Id, "same");
        Assert.All(time.Timers, timer => Assert.True(timer.Disposed));
    }

    [Fact]
    public async Task Concurrent_reminder_replacements_leave_only_one_owned_timer_and_cancel_disposes_all()
    {
        var time = new ControlledTimeProvider();
        using var scheduler = new CoreActorReminderScheduler(new CallbackRuntime(), Serializer, time);
        await scheduler.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.FromSeconds(1), "{}");

        await RaceReplacementDuringDisposalAsync(Assert.Single(time.Timers),
            () => scheduler.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.FromSeconds(1), "{}").AsTask());
        await RaceAsync(32, _ => scheduler.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.FromSeconds(1), "{}").AsTask());

        Assert.Single(time.Timers, timer => !timer.Disposed);
        Assert.Single(await scheduler.ListAsync("A"));
        await scheduler.CancelAsync("A", Id, "same");
        Assert.All(time.Timers, timer => Assert.True(timer.Disposed));
    }

    [Fact]
    public async Task Concurrent_reminder_create_without_overwrite_has_one_winner_and_never_arms_rejected_timers()
    {
        var time = new ControlledTimeProvider();
        using var scheduler = new CoreActorReminderScheduler(new CallbackRuntime(), Serializer, time);
        var accepted = 0;
        var rejected = 0;
        await RaceAsync(32, async _ =>
        {
            try
            {
                await scheduler.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.Zero, "{}", overwrite: false);
                Interlocked.Increment(ref accepted);
            }
            catch (ActorReminderAlreadyExistsException)
            {
                Interlocked.Increment(ref rejected);
            }
        });

        Assert.Equal(1, accepted);
        Assert.Equal(31, rejected);
        Assert.Single(time.Timers);
        await scheduler.CancelAllAsync("A");
        Assert.All(time.Timers, timer => Assert.True(timer.Disposed));
    }

    [Fact]
    public async Task Registration_racing_dispose_cannot_publish_an_orphan_timer_or_reminder()
    {
        var time = new ControlledTimeProvider();
        using var timers = new CoreActorTimerScheduler(new CallbackRuntime(), Serializer, time);
        using var reminders = new CoreActorReminderScheduler(new CallbackRuntime(), Serializer, time);
        await RaceAsync(32, async index =>
        {
            if (index == 0)
            {
                timers.Dispose();
                reminders.Dispose();
                return;
            }

            try
            {
                await timers.ScheduleAsync("A", Id, index.ToString(), TimeSpan.Zero, "Tick", "{}");
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                await reminders.ScheduleAsync("A", Id, index.ToString(), TimeSpan.Zero, TimeSpan.Zero, "{}");
            }
            catch (ObjectDisposedException)
            {
            }
        });

        Assert.All(time.Timers, timer => Assert.True(timer.Disposed));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            timers.ScheduleAsync("A", Id, "late", TimeSpan.Zero, "Tick", "{}").AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            reminders.ScheduleAsync("A", Id, "late", TimeSpan.Zero, TimeSpan.Zero, "{}").AsTask());
    }

    [Fact]
    public async Task Inline_provider_callbacks_are_published_before_dispatch_and_never_hold_the_registration_lock()
    {
        var time = new ControlledTimeProvider { FireOnChange = true };
        var finished = Signal();
        CoreActorReminderScheduler? scheduler = null;
        var runtime = new CallbackRuntime(async _ =>
        {
            try
            {
                await Task.Run(async () =>
                {
                    Assert.NotNull(await scheduler!.GetAsync("A", Id, "Tick"));
                    await scheduler.CancelAsync("A", Id, "Tick");
                }).WaitAsync(TestTimeout);
                finished.TrySetResult();
            }
            catch (Exception exception)
            {
                finished.TrySetException(exception);
            }

            return null;
        });
        using (scheduler = new CoreActorReminderScheduler(runtime, Serializer, time))
        {
            await scheduler.ScheduleAsync("A", Id, "Tick", TimeSpan.Zero, TimeSpan.Zero, "{}");
            await finished.Task.WaitAsync(TestTimeout);
            Assert.All(time.Timers, timer =>
            {
                Assert.Equal(Timeout.InfiniteTimeSpan, timer.InitialDueTime);
                Assert.True(timer.Disposed);
            });
        }
    }

    [Fact]
    public async Task Cancelled_operations_do_not_replace_or_remove_existing_registrations()
    {
        var time = new ControlledTimeProvider();
        using var timers = new CoreActorTimerScheduler(new CallbackRuntime(), Serializer, time);
        using var reminders = new CoreActorReminderScheduler(new CallbackRuntime(), Serializer, time);
        await timers.ScheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", "{}");
        await reminders.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.Zero, "{}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timers.ScheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", "{}", cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timers.ScheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", new byte[0], cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timers.ScheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", 1, cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timers.RescheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", "{}", cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timers.CancelAsync("A", Id, "same", token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reminders.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.Zero, "{}", cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reminders.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.Zero, new byte[0], cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reminders.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.Zero, 1, cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reminders.CancelAsync("A", Id, "same", token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reminders.CancelAllAsync("A", cancellationToken: token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reminders.GetAsync("A", Id, "same", token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reminders.ListAsync("A", cancellationToken: token).AsTask());
        Assert.Equal(2, time.Timers.Count);
        Assert.All(time.Timers, timer => Assert.False(timer.Disposed));
    }

    [Fact]
    public async Task Timer_creation_suppresses_context_without_changing_the_callers_flow_state()
    {
        var time = new ControlledTimeProvider();
        using var timers = new CoreActorTimerScheduler(new CallbackRuntime(), Serializer, time);
        using var reminders = new CoreActorReminderScheduler(new CallbackRuntime(), Serializer, time);
        Assert.False(ExecutionContext.IsFlowSuppressed());
        await timers.ScheduleAsync("A", Id, "timer", TimeSpan.Zero, "Tick", "{}");
        await reminders.ScheduleAsync("A", Id, "reminder", TimeSpan.Zero, TimeSpan.Zero, "{}");
        Assert.False(ExecutionContext.IsFlowSuppressed());
        using (ExecutionContext.SuppressFlow())
        {
            Assert.True(timers.ScheduleAsync("A", Id, "suppressed", TimeSpan.Zero, "Tick", "{}").IsCompletedSuccessfully);
            Assert.True(reminders.ScheduleAsync("A", Id, "suppressed", TimeSpan.Zero, TimeSpan.Zero, "{}").IsCompletedSuccessfully);
            Assert.True(ExecutionContext.IsFlowSuppressed());
        }

        Assert.All(time.Timers, timer => Assert.True(timer.CreatedWithFlowSuppressed));
    }

    [Fact]
    public async Task Failed_arming_preserves_the_previous_timer_and_reminder()
    {
        var time = new ControlledTimeProvider();
        using var timers = new CoreActorTimerScheduler(new CallbackRuntime(), Serializer, time);
        using var reminders = new CoreActorReminderScheduler(new CallbackRuntime(), Serializer, time);
        await timers.ScheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", "{}");
        await reminders.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.Zero, "\"original\"");
        var original = time.Timers.ToArray();
        time.FailChange = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => timers.RescheduleAsync("A", Id, "same", TimeSpan.Zero, "Tick", "{}").AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => reminders.ScheduleAsync("A", Id, "same", TimeSpan.Zero, TimeSpan.Zero, "\"replacement\"").AsTask());

        Assert.All(original, timer => Assert.False(timer.Disposed));
        Assert.All(time.Timers.Except(original), timer => Assert.True(timer.Disposed));
        Assert.Equal("\"original\"", (await reminders.GetAsync("A", Id, "same"))!.ArgumentsJson);
    }

    [Fact]
    public async Task Timer_headers_are_snapshotted_and_period_and_ttl_are_preserved()
    {
        var time = new ControlledTimeProvider();
        var received = new TaskCompletionSource<ActorRuntimeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scheduler = new CoreActorTimerScheduler(new CallbackRuntime(request =>
        {
            received.TrySetResult(request);
            return Task.FromResult<byte[]?>(null);
        }), Serializer, time);
        var headers = new Dictionary<string, string> { ["custom"] = "original", ["Dapr-Reentrancy-Id"] = "chain" };
        await scheduler.ScheduleAsync("A", Id, "same", TimeSpan.FromSeconds(1), "Tick", "{}", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), headers);
        headers["custom"] = "changed";
        var timer = Assert.Single(time.Timers);
        Assert.Equal(TimeSpan.FromSeconds(1), timer.DueTime);
        Assert.Equal(TimeSpan.FromSeconds(2), timer.Period);
        time.Now += TimeSpan.FromSeconds(3);
        timer.Fire();
        var request = await received.Task.WaitAsync(TestTimeout);
        Assert.Equal("original", request.Headers["custom"]);
        Assert.DoesNotContain(request.Headers, pair => pair.Value == "chain");
        time.Now += TimeSpan.FromTicks(1);
        timer.Fire();
        await timer.DisposedSignal.Task.WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task Asynchronous_timer_and_reminder_dispatch_failures_are_observed_and_logged()
    {
        var time = new ControlledTimeProvider();
        var failure = new InvalidOperationException("callback failed");
        var runtime = new CallbackRuntime(async _ =>
        {
            await Task.Yield();
            throw failure;
        });
        var timerLogger = new ErrorLogger<CoreActorTimerScheduler>();
        var reminderLogger = new ErrorLogger<CoreActorReminderScheduler>();
        using var timers = new CoreActorTimerScheduler(runtime, Serializer, time, timerLogger);
        using var reminders = new CoreActorReminderScheduler(runtime, Serializer, time, reminderLogger);
        await timers.ScheduleAsync("A", Id, "Tick", TimeSpan.Zero, "Tick", "{}");
        await reminders.ScheduleAsync("A", Id, "Tick", TimeSpan.Zero, TimeSpan.Zero, "{}");
        foreach (var timer in time.Timers)
        {
            timer.Fire();
        }

        Assert.Same(failure, await timerLogger.Error.Task.WaitAsync(TestTimeout));
        Assert.Same(failure, await reminderLogger.Error.Task.WaitAsync(TestTimeout));
    }

    private static async Task RaceAsync(int count, Func<int, Task> operation)
    {
        var start = Signal();
        var tasks = Enumerable.Range(0, count).Select(async index =>
        {
            await start.Task;
            await operation(index);
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(tasks).WaitAsync(TestTimeout);
    }

    private static async Task RaceReplacementDuringDisposalAsync(ControlledTimer original, Func<Task> replace)
    {
        var disposing = Signal();
        using var release = new ManualResetEventSlim();
        var disposeCount = 0;
        original.BeforeDispose = () =>
        {
            if (Interlocked.Increment(ref disposeCount) == 1)
            {
                disposing.TrySetResult();
                if (!release.Wait(TestTimeout))
                {
                    throw new TimeoutException("Replacement disposal was not released.");
                }
            }
        };
        var first = Task.Run(replace);
        Task? second = null;
        try
        {
            await disposing.Task.WaitAsync(TestTimeout);
            second = Task.Run(replace);
            // The correct implementation blocks the second replacement on its registration lock.
            // Give an unprotected replacement a chance to finish, without requiring either
            // implementation to enter the same Dispose barrier twice.
            await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            release.Set();
            await first.WaitAsync(TestTimeout);
            if (second is not null)
            {
                await second.WaitAsync(TestTimeout);
            }
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class GatedActor : IActor
    {
        public readonly TaskCompletionSource Release = Signal();
        public readonly TaskCompletionSource CallbackEntered = Signal();
        public int Value;
        public bool Holding;
        public bool Overlapped;
        public IReadOnlyDictionary<string, string>? CallbackHeaders;
    }

    private sealed class GatedDispatcher(GatedActor actor) : IActorDispatcher
    {
        public Func<IReadOnlyDictionary<string, string>, ValueTask> Schedule { get; set; } = null!;

        public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor _, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            if (request.MethodName == "ScheduleAndHold")
            {
                var previous = actor.Value;
                actor.Holding = true;
                await Schedule(request.Headers);
                await actor.Release.Task.WaitAsync(cancellationToken);
                actor.Value = previous + 1;
                actor.Holding = false;
            }
            else
            {
                actor.CallbackHeaders = request.Headers;
                actor.Overlapped = actor.Holding;
                actor.Value++;
                actor.CallbackEntered.TrySetResult();
            }

            return new ActorDispatchResponse(BitConverter.GetBytes(actor.Value));
        }
    }

    private sealed class CallbackRuntime(Func<ActorRuntimeRequest, Task<byte[]?>>? callback = null) : IActorRuntime
    {
        public Task<byte[]?> DispatchAsync(ActorRuntimeRequest request, CancellationToken cancellationToken = default) =>
            callback?.Invoke(request) ?? Task.FromResult<byte[]?>(null);

        public Task<byte[]?> InvokeAsync(string actorType, string actorId, string methodName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeactivateAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ControlledTimeProvider : TimeProvider
    {
        public ConcurrentBag<ControlledTimer> Timers { get; } = [];
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public bool FireOnChange { get; init; }
        public bool FailChange { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ControlledTimer(this, callback, state, dueTime);
            Timers.Add(timer);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                timer.Change(dueTime, period);
            }

            return timer;
        }
    }

    private sealed class ControlledTimer(ControlledTimeProvider provider, TimerCallback callback, object? state, TimeSpan initialDueTime) : ITimer
    {
        private int disposed;
        public TimeSpan InitialDueTime { get; } = initialDueTime;
        public bool CreatedWithFlowSuppressed { get; } = ExecutionContext.IsFlowSuppressed();
        public Action? BeforeDispose { get; set; }
        public TimeSpan DueTime { get; private set; }
        public TimeSpan Period { get; private set; }
        public bool Disposed => Volatile.Read(ref disposed) != 0;
        public TaskCompletionSource DisposedSignal { get; } = Signal();

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (provider.FailChange)
            {
                throw new InvalidOperationException("Cannot arm timer.");
            }

            DueTime = dueTime;
            Period = period;
            if (provider.FireOnChange)
            {
                Fire();
            }

            return !Disposed;
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            BeforeDispose?.Invoke();
            Thread.Yield();
            Interlocked.Exchange(ref disposed, 1);
            DisposedSignal.TrySetResult();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ErrorLogger<T> : ILogger<T>
    {
        public TaskCompletionSource<Exception> Error { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception is not null)
            {
                Error.TrySetResult(exception);
            }
        }
    }
}
