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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Test;

public sealed class DeterministicConcurrencyTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Reentrant_call_chain_does_not_deadlock_or_double_apply()
    {
        await using var runtime = CreateRuntime(new SeededRandomActorScheduler(12));
        var call = runtime.InvokeAsync(
            "Nasty",
            ActorId.Create("chain"),
            "StartReentrant");

        await runtime.RunToIdle();

        Assert.Equal(1, int.Parse(System.Text.Encoding.UTF8.GetString((await call)!)));
        Assert.Equal(["StartReentrant"], runtime.Transcript.Select(entry => entry.OperationName).ToArray());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Timer_racing_inbound_message_loses_neither_update()
    {
        await using var runtime = CreateRuntime(new PriorityActorScheduler(24));
        var id = ActorId.Create("race");
        runtime.Time.ScheduleTimer("Nasty", id, "Timer", TimeSpan.FromSeconds(1));
        var inbound = runtime.InvokeAsync("Nasty", id, "ApplyOnce", "\"message\"");

        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();

        Assert.NotNull(await inbound);
        Assert.Contains(runtime.Transcript, entry => entry.OperationName == "ApplyOnce");
        Assert.Contains(runtime.Transcript, entry => entry.OperationName == "Timer");
        var read = runtime.InvokeAsync("Nasty", id, "ApplyOnce", "\"read\"");
        await runtime.RunToIdle();
        Assert.Equal(12, int.Parse(System.Text.Encoding.UTF8.GetString((await read)!)));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Same_actor_id_non_reentrant_turns_do_not_overlap()
    {
        var probe = new TurnSerializationProbe();
        await using var runtime = CreateRuntime(new SeededRandomActorScheduler(44), probe);
        var id = ActorId.Create("serialized");

        var first = runtime.InvokeAsync("Nasty", id, "Hold");
        Assert.True(await runtime.StepAsync());
        await probe.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        var second = runtime.InvokeAsync("Nasty", id, "ApplyOnce", "\"second\"");
        Assert.False(await runtime.StepAsync());
        Assert.False(second.IsCompleted);

        probe.Release();
        await runtime.RunToIdle();

        Assert.NotNull(await first);
        Assert.Equal(1, int.Parse(System.Text.Encoding.UTF8.GetString((await second)!)));
        Assert.Equal(1, probe.MaxActive);
        Assert.Equal(["Hold", "ApplyOnce"], runtime.Transcript.Select(entry => entry.OperationName).ToArray());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Reminder_due_during_method_turn_waits_until_method_turn_commits()
    {
        var probe = new TurnSerializationProbe();
        await using var runtime = CreateRuntime(new SeededRandomActorScheduler(45), probe);
        var id = ActorId.Create("reminder-serialized");

        var method = runtime.InvokeAsync("Nasty", id, "HoldAndApply", "\"method\"");
        Assert.True(await runtime.StepAsync());
        await probe.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        runtime.Time.ScheduleReminder("Nasty", id, "Reminder", TimeSpan.FromSeconds(1));
        runtime.Time.Advance(TimeSpan.FromSeconds(1));

        Assert.False(await runtime.StepAsync());

        var readBeforeRelease = runtime.InvokeAsync("Nasty", id, "ReadApplied");
        Assert.False(await runtime.StepAsync());
        Assert.False(readBeforeRelease.IsCompleted);

        probe.Release();
        await runtime.RunToIdle();

        Assert.Equal(1, int.Parse(System.Text.Encoding.UTF8.GetString((await method)!)));
        Assert.Equal(11, int.Parse(System.Text.Encoding.UTF8.GetString((await readBeforeRelease)!)));
        Assert.Equal(1, probe.MaxActive);
        Assert.Equal(["HoldAndApply", "Reminder", "ReadApplied"], runtime.Transcript.Select(entry => entry.OperationName).ToArray());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Fault_mid_state_write_recovers_without_double_apply()
    {
        await using var runtime = CreateRuntime(new SeededRandomActorScheduler(33));
        var id = ActorId.Create("fault-recovery");
        runtime.Faults.FailNextStateWrite<NastyState>();

        var first = runtime.InvokeAsync("Nasty", id, "ApplyOnce", "\"op-1\"");
        await runtime.RunToIdle();
        await Assert.ThrowsAsync<ActorInjectedTransientException>(() => first);

        var retry = runtime.InvokeAsync("Nasty", id, "ApplyOnce", "\"op-1\"");
        await runtime.RunToIdle();
        Assert.Equal(1, int.Parse(System.Text.Encoding.UTF8.GetString((await retry)!)));

        var next = runtime.InvokeAsync("Nasty", id, "ApplyOnce", "\"op-2\"");
        await runtime.RunToIdle();
        Assert.Equal(2, int.Parse(System.Text.Encoding.UTF8.GetString((await next)!)));
    }

    private static ActorTestRuntime CreateRuntime(ControlledActorScheduler scheduler, TurnSerializationProbe? probe = null) =>
        new(services => services.AddDaprActorsCore(registrations =>
        {
            services.AddSingleton(probe ?? new TurnSerializationProbe());
            registrations.Add(
                "Nasty",
                typeof(INastyActor),
                typeof(NastyActor),
                (sp, _) => new NastyActor(
                    sp.GetRequiredService<ActorActivationContext>(),
                    sp.GetRequiredService<IActorInvocationClient>(),
                    sp.GetRequiredService<TurnSerializationProbe>()),
                new NastyActorDispatcher(),
                new ActorLifecycle(
                    (actor, cancellationToken) => ((Actor)actor).InvokeOnActivateAsync(cancellationToken),
                    (actor, cancellationToken) => ((Actor)actor).InvokeOnDeactivateAsync(cancellationToken),
                    (actor, context, cancellationToken) => ((Actor)actor).InvokeOnPreActorMethodAsync(context, cancellationToken),
                    (actor, context, exception, cancellationToken) => ((Actor)actor).InvokeOnPostActorMethodAsync(context, exception, cancellationToken)));
        }), new ActorTestRuntimeOptions { Scheduler = scheduler });
}
