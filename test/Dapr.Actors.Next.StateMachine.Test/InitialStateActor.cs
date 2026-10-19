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
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.StateMachine.Test;

[GenerateActorClient]
public interface IInitialStateActor : IActor
{
    Task<InitialSnapshot> Read(CancellationToken cancellationToken = default);
    Task<string> Command(CancellationToken cancellationToken = default);
    Task Extend(CancellationToken cancellationToken = default);
    Task ScheduleCustom(CancellationToken cancellationToken = default);
    Task CancelCustom(CancellationToken cancellationToken = default);
    Task Finish(CancellationToken cancellationToken = default);
    Task Restart(CancellationToken cancellationToken = default);
    Task RestartParent(CancellationToken cancellationToken = default);
    Task FailCommand(CancellationToken cancellationToken = default);
}

public enum InitialState { Parent, Open, Done }

public sealed record InitialData(string[] Log);

public sealed record InitialSnapshot(InitialState State, InitialData Data);

public sealed record InitialFollowUp;

public sealed record InitialCommand;

public sealed record ExtendInitial;

public sealed record ScheduleInitialCustom;

public sealed record CancelInitialCustom;

public sealed record FinishInitial;
public sealed record RestartInitial;
public sealed record RestartInitialParent;
public sealed record FailInitialCommand;

public sealed class InitialEntryProbe
{
    public int Attempts { get; set; }
    public bool FailEntry { get; set; }
    public bool FailFollowUp { get; set; }
    public bool RaiseFromActor { get; set; }
    public bool FinishFromEntry { get; set; }
    public bool CancelEntry { get; set; }
    public int Exits { get; set; }
    public int ParentExits { get; set; }
}

[DaprActor("InitialState")]
public sealed class InitialStateActor(
    ActorActivationContext context,
    IActorTimerScheduler timers,
    InitialEntryProbe probe)
    : StateMachineActor<InitialState, InitialData>(context, timers, "InitialState", new InitialData([])),
        IInitialStateActor
{
    protected override void Configure(IStateMachine<InitialState, InitialData> sm)
    {
        sm.InitialState(InitialState.Open);
        sm.In(InitialState.Parent)
            .OnEntry(ctx => ctx.Update(data => data with { Log = [.. data.Log, "parent"] }))
            .OnExit(_ => probe.ParentExits++);
        sm.In(InitialState.Parent).On<RestartInitialParent>().GoTo(InitialState.Open);
        sm.In(InitialState.Open).On<RestartInitial>().GoTo(InitialState.Open);
        sm.In(InitialState.Open).On<FailInitialCommand>().Do(ctx =>
        {
            ctx.Raise(new InitialFollowUp());
            throw new InvalidOperationException("command failed");
        });
        sm.In(InitialState.Open)
            .SubstateOf(InitialState.Parent)
            .OnExit(_ => probe.Exits++)
            .OnEntry(async ctx =>
            {
                probe.Attempts++;
                await Task.Yield();
                ctx.Update(data => data with { Log = [.. data.Log, "child"] });
                if (probe.RaiseFromActor)
                {
                    await Raise<object?>(new InitialFollowUp());
                }
                else
                {
                    ctx.Raise(new InitialFollowUp());
                }

                if (probe.FailEntry)
                {
                    probe.FailEntry = false;
                    throw new InvalidOperationException("entry failed");
                }

                if (probe.CancelEntry)
                {
                    probe.CancelEntry = false;
                    throw new OperationCanceledException("entry canceled");
                }
            })
            .After(TimeSpan.FromSeconds(10))
            .On<InitialFollowUp>().Do(ctx =>
            {
                if (probe.FailFollowUp)
                {
                    probe.FailFollowUp = false;
                    throw new InvalidOperationException("follow-up failed");
                }

                ctx.Update(data => data with { Log = [.. data.Log, "queued"] });
                if (probe.FinishFromEntry)
                {
                    ctx.Raise(new FinishInitial());
                }

                ctx.Reply("entry-reply");
            });
        sm.In(InitialState.Open)
            .On<InitialCommand>().Do(ctx => ctx.Update(data => data with { Log = [.. data.Log, "command"] }))
            .Reply("command-reply");
        sm.In(InitialState.Open)
            .On<ExtendInitial>().Do(ctx =>
                ctx.Timers.Reschedule(StateMachineConstants.StateTimeoutTimerName, TimeSpan.FromSeconds(10)));
        sm.In(InitialState.Open)
            .On<ScheduleInitialCustom>().Do(ctx => ctx.Timers.Schedule("custom", TimeSpan.FromSeconds(1)));
        sm.In(InitialState.Open)
            .On<CancelInitialCustom>().Do(ctx => ctx.Timers.Cancel("custom"));
        sm.In(InitialState.Open)
            .On<StateMachineTimerFired>().Do(ctx =>
                ctx.Update(data => data with { Log = [.. data.Log, $"timer:{ctx.Event.Name}"] }));
        sm.In(InitialState.Open)
            .On<StateTimeout<InitialState>>().GoTo(InitialState.Done);
        sm.In(InitialState.Open)
            .On<FinishInitial>().GoTo(InitialState.Done);
        sm.In(InitialState.Done)
            .OnEntry(ctx => ctx.Update(data => data with { Log = [.. data.Log, "done"] }));
    }

    public Task<InitialSnapshot> Read(CancellationToken cancellationToken = default) =>
        Task.FromResult(new InitialSnapshot(CurrentState, Data));

    public Task<string> Command(CancellationToken cancellationToken = default) =>
        Raise<string>(new InitialCommand(), cancellationToken);

    public Task Extend(CancellationToken cancellationToken = default) =>
        Raise<object?>(new ExtendInitial(), cancellationToken);

    public Task ScheduleCustom(CancellationToken cancellationToken = default) =>
        Raise<object?>(new ScheduleInitialCustom(), cancellationToken);

    public Task CancelCustom(CancellationToken cancellationToken = default) =>
        Raise<object?>(new CancelInitialCustom(), cancellationToken);

    public Task Finish(CancellationToken cancellationToken = default) =>
        Raise<object?>(new FinishInitial(), cancellationToken);
    public Task Restart(CancellationToken cancellationToken = default) =>
        Raise<object?>(new RestartInitial(), cancellationToken);
    public Task RestartParent(CancellationToken cancellationToken = default) =>
        Raise<object?>(new RestartInitialParent(), cancellationToken);
    public Task FailCommand(CancellationToken cancellationToken = default) =>
        Raise<object?>(new FailInitialCommand(), cancellationToken);
}
