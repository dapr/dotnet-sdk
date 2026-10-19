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

using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.StateMachine.Test;

public sealed class InitialStateTests
{
    private static readonly ActorId Id = ActorId.Create("initial");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Failed_command_does_not_leak_raised_events_into_next_command()
    {
        await using var runtime = CreateRuntime();
        await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        await Assert.ThrowsAnyAsync<Exception>(() => Invoke<object?>(runtime, nameof(IInitialStateActor.FailCommand)));
        await Invoke<string>(runtime, nameof(IInitialStateActor.Command));
        Assert.Equal(["parent", "child", "queued", "command"],
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task External_self_transition_reenters_and_restarts_timeout(bool inherited)
    {
        var probe = new InitialEntryProbe();
        await using var runtime = CreateRuntime(probe);
        await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        runtime.Time.Advance(TimeSpan.FromSeconds(9));
        await Invoke<object?>(runtime, inherited ? nameof(IInitialStateActor.RestartParent) : nameof(IInitialStateActor.Restart));
        Assert.Equal(2, probe.Attempts);
        Assert.Equal(1, probe.Exits);
        Assert.Equal(inherited ? 1 : 0, probe.ParentExits);
        var log = (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log;
        Assert.Equal(inherited ? new[] { "parent", "child", "queued", "parent", "child", "queued" }
            : ["parent", "child", "queued", "child", "queued"], log);
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(InitialState.Open, (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).State);
        runtime.Time.Advance(TimeSpan.FromSeconds(9));
        await runtime.RunToIdle();
        Assert.Equal(InitialState.Done, (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).State);
    }

    [Fact]
    public async Task Initial_hierarchy_and_queued_events_run_once_and_persist_even_for_query()
    {
        var probe = new InitialEntryProbe();
        await using var runtime = CreateRuntime(probe);
        var snapshot = await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));

        Assert.Equal(InitialState.Open, snapshot.State);
        Assert.Equal(["parent", "child", "queued"], snapshot.Data.Log);
        Assert.Equal(snapshot.Data.Log, runtime.StateOf("InitialState", Id).Data<InitialData>()!.Log);
        await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        Assert.Equal(1, probe.Attempts);

        var deactivate = runtime.DeactivateAsync("InitialState", Id);
        await runtime.RunToIdle();
        await deactivate;
        var restored = await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        Assert.Equal(snapshot.Data.Log, restored.Data.Log);
        Assert.Equal(1, probe.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initial_raised_events_finish_before_command_and_do_not_replace_its_reply(bool raiseFromActor)
    {
        await using var runtime = CreateRuntime(new InitialEntryProbe { RaiseFromActor = raiseFromActor });
        Assert.Equal("command-reply", await Invoke<string>(runtime, nameof(IInitialStateActor.Command)));
        Assert.Equal(["parent", "child", "queued", "command"],
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Failed_initialization_retries_without_leaking_queued_events_or_partial_data(bool failEntry)
    {
        var probe = new InitialEntryProbe { FailEntry = failEntry, FailFollowUp = !failEntry };
        await using var runtime = CreateRuntime(probe);
        var failure =
            await Assert.ThrowsAnyAsync<Exception>(() =>
                Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read)));
        Assert.IsType<InvalidOperationException>(failure.GetBaseException());
        Assert.Contains(failEntry ? "entry failed" : "follow-up failed", failure.GetBaseException().Message);
        Assert.Null(runtime.StateOf("InitialState", Id)
            .Get<StateMachineEnvelope<InitialState, InitialData>>(StateMachineConstants.EnvelopeStateName));

        Assert.Equal(["parent", "child", "queued"],
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log);
        Assert.Equal(2, probe.Attempts);
        runtime.Time.Advance(TimeSpan.FromSeconds(10));
        await runtime.RunToIdle();
        Assert.Equal(InitialState.Done,
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).State);
    }

    [Fact]
    public async Task Initial_timeout_reaches_generated_dispatcher_and_persists_transition()
    {
        await using var runtime = CreateRuntime();
        await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        runtime.Time.Advance(TimeSpan.FromSeconds(9));
        await runtime.RunToIdle();
        Assert.Equal(InitialState.Open,
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).State);
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        var snapshot = await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        Assert.Equal(InitialState.Done, snapshot.State);
        Assert.Equal(["parent", "child", "queued", "done"], snapshot.Data.Log);
        Assert.Equal(InitialState.Done, runtime.StateOf("InitialState", Id).CurrentState<InitialState>());
    }

    [Fact]
    public async Task Initial_raised_transition_finishes_and_persists_before_first_query()
    {
        await using var runtime = CreateRuntime(new InitialEntryProbe { FinishFromEntry = true });
        var snapshot = await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        Assert.Equal(InitialState.Done, snapshot.State);
        Assert.Equal(["parent", "child", "queued", "done"], snapshot.Data.Log);
        Assert.Equal(InitialState.Done, runtime.StateOf("InitialState", Id).CurrentState<InitialState>());
    }

    [Fact]
    public async Task Canceled_initial_entry_can_be_retried()
    {
        var probe = new InitialEntryProbe { CancelEntry = true };
        await using var runtime = CreateRuntime(probe);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read)));
        Assert.Null(runtime.StateOf("InitialState", Id).Data<InitialData>());
        Assert.Equal(["parent", "child", "queued"],
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log);
        Assert.Equal(2, probe.Attempts);
    }

    [Fact]
    public async Task Failed_initial_state_flush_retries_pending_writes_without_replaying_entry()
    {
        var probe = new InitialEntryProbe();
        await using var runtime = CreateRuntime(probe);
        runtime.Faults.FailNextStateWrite<StateMachineEnvelope<InitialState, InitialData>>();
        var failure =
            await Assert.ThrowsAnyAsync<Exception>(() =>
                Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read)));
        Assert.Contains("Injected state write failure", failure.Message);
        Assert.Null(runtime.StateOf("InitialState", Id)
            .Get<StateMachineEnvelope<InitialState, InitialData>>(StateMachineConstants.EnvelopeStateName));
        var snapshot = await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        Assert.Equal(["parent", "child", "queued"], snapshot.Data.Log);
        Assert.Equal(snapshot.Data.Log, runtime.StateOf("InitialState", Id).Data<InitialData>()!.Log);
        Assert.Equal(1, probe.Attempts);
    }

    [Fact]
    public async Task Restored_envelope_does_not_replay_entries_or_restart_initial_timeout()
    {
        var probe = new InitialEntryProbe();
        await using var runtime = CreateRuntime(probe);
        await runtime.SeedStateAsync("InitialState", Id, StateMachineConstants.EnvelopeStateName,
            new StateMachineEnvelope<InitialState, InitialData>(InitialState.Open, new InitialData(["restored"]), []),
            ActorStateSeedForm.Plain);
        Assert.Equal(["restored"], (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log);
        runtime.Time.Advance(TimeSpan.FromSeconds(20));
        await runtime.RunToIdle();
        Assert.Equal(InitialState.Open,
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).State);
        Assert.Equal(0, probe.Attempts);
    }

    [Fact]
    public async Task Rescheduled_timeout_ignores_original_deadline()
    {
        await using var runtime = CreateRuntime();
        await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read));
        runtime.Time.Advance(TimeSpan.FromSeconds(9));
        await Invoke<object?>(runtime, nameof(IInitialStateActor.Extend));
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(InitialState.Open,
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).State);
        runtime.Time.Advance(TimeSpan.FromSeconds(9));
        await runtime.RunToIdle();
        Assert.Equal(InitialState.Done,
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).State);
    }

    [Fact]
    public async Task Custom_timer_fires_through_generated_dispatcher_and_can_be_canceled()
    {
        await using var runtime = CreateRuntime();
        await Invoke<object?>(runtime, nameof(IInitialStateActor.ScheduleCustom));
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(["parent", "child", "queued", "timer:custom"],
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log);
        await Invoke<object?>(runtime, nameof(IInitialStateActor.ScheduleCustom));
        await Invoke<object?>(runtime, nameof(IInitialStateActor.CancelCustom));
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Single((await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log,
            value => value == "timer:custom");
    }

    [Fact]
    public async Task Exiting_initial_state_cancels_timeout_and_stale_callback_is_ignored()
    {
        await using var runtime = CreateRuntime();
        await Invoke<object?>(runtime, nameof(IInitialStateActor.Finish));
        runtime.Time.Advance(TimeSpan.FromSeconds(10));
        await runtime.RunToIdle();
        var stale = runtime.InvokeAsync("InitialState", Id, StateMachineConstants.TimerOperationName,
            JsonSerializer.Serialize(new
            {
                Name = StateMachineConstants.StateTimeoutTimerName,
                State = nameof(InitialState.Open)
            }));
        await runtime.RunToIdle();
        await stale;
        Assert.Equal(["parent", "child", "queued", "done"],
            (await Invoke<InitialSnapshot>(runtime, nameof(IInitialStateActor.Read))).Data.Log);
    }

    private static ActorTestRuntime CreateRuntime(InitialEntryProbe? probe = null) =>
        new(services =>
        {
            services.AddSingleton(probe ?? new InitialEntryProbe());
            services.AddDaprActors(_ => { });
        });

    private static async Task<T> Invoke<T>(ActorTestRuntime runtime, string operation)
    {
        var call = runtime.InvokeAsync("InitialState", Id, operation);
        await runtime.RunToIdle();
        var bytes = await call;
        return bytes is { Length: > 0 } ? JsonSerializer.Deserialize<T>(bytes, JsonOptions)! : default!;
    }
}
