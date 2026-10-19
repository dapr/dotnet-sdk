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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.StateMachine;

namespace Dapr.IntegrationTest.Actors.Next;

[GenerateActorClient]
public interface IGeneratedDeadlineActor : IActor
{
    Task<GeneratedDeadlineSnapshot> ReadAsync(CancellationToken cancellationToken = default);
    Task RestartAsync(CancellationToken cancellationToken = default);
    Task FailAsync(CancellationToken cancellationToken = default);
    Task CommandAsync(CancellationToken cancellationToken = default);
}
public sealed record RestartDeadline;
public sealed record FailDeadline;
public sealed record DeadlineCommand;

public enum GeneratedDeadlineState { Waiting, Done }

public sealed record GeneratedDeadlineData(int Entries);

public sealed record GeneratedDeadlineSnapshot(GeneratedDeadlineState State, int Entries);

[DaprActor("GeneratedDeadline")]
public sealed class GeneratedDeadlineActor(ActorActivationContext context, IActorTimerScheduler timers)
    : StateMachineActor<GeneratedDeadlineState, GeneratedDeadlineData>(context, timers, "GeneratedDeadline", new(0)),
        IGeneratedDeadlineActor
{
    protected override void Configure(IStateMachine<GeneratedDeadlineState, GeneratedDeadlineData> stateMachine)
    {
        stateMachine.InitialState(GeneratedDeadlineState.Waiting);
        stateMachine.In(GeneratedDeadlineState.Waiting)
            .OnEntry(context => context.Update(data => data with { Entries = data.Entries + 1 }))
            .After(TimeSpan.FromMilliseconds(500))
            .On<StateTimeout<GeneratedDeadlineState>>().GoTo(GeneratedDeadlineState.Done);
        stateMachine.In(GeneratedDeadlineState.Done).Ignore<StateTimeout<GeneratedDeadlineState>>();
        foreach (var state in new[] { GeneratedDeadlineState.Waiting, GeneratedDeadlineState.Done })
        {
            stateMachine.In(state).On<RestartDeadline>().GoTo(GeneratedDeadlineState.Waiting);
            stateMachine.In(state).On<DeadlineCommand>().Do(_ => { });
            stateMachine.In(state).On<FailDeadline>().Do(context =>
            {
                context.Raise(new RestartDeadline());
                throw new InvalidOperationException("command intentionally failed");
            });
        }
    }

    public Task<GeneratedDeadlineSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new GeneratedDeadlineSnapshot(CurrentState, Data.Entries));
    public Task RestartAsync(CancellationToken cancellationToken = default) => Raise<object?>(new RestartDeadline(), cancellationToken);
    public Task FailAsync(CancellationToken cancellationToken = default) => Raise<object?>(new FailDeadline(), cancellationToken);
    public Task CommandAsync(CancellationToken cancellationToken = default) => Raise<object?>(new DeadlineCommand(), cancellationToken);
}

[GenerateActorClient]
public interface IGeneratedActivationActor : IActor
{
    Task<int> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed class GeneratedActivationProbe
{
    public int Attempts;
    public int Disposals;
}

[DaprActor("GeneratedActivation")]
public sealed class GeneratedActivationActor(ActorActivationContext context, GeneratedActivationProbe probe)
    : Actor, IGeneratedActivationActor, IDisposable
{
    private int activation;
    protected override ActorId Id => context.ActorId;
    protected override IActorStateAccessor State => context.State;

    protected override ValueTask OnActivateAsync(CancellationToken cancellationToken = default)
    {
        activation = Interlocked.Increment(ref probe.Attempts);
        if (activation == 1)
        {
            throw new InvalidOperationException("First activation intentionally fails.");
        }

        return ValueTask.CompletedTask;
    }

    public Task<int> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(activation);
    public void Dispose() => Interlocked.Increment(ref probe.Disposals);
}

[GenerateActorClient]
public interface IGeneratedHierarchyActor : IActor
{
    Task QueueAsync(CancellationToken cancellationToken = default);
    Task OpenAsync(CancellationToken cancellationToken = default);
    Task NextAsync(CancellationToken cancellationToken = default);
    Task<int> CommandAsync(CancellationToken cancellationToken = default);
    Task<int> ReadAsync(CancellationToken cancellationToken = default);
}

public enum GeneratedHierarchyState { Waiting, IgnoreParent, IgnoreChild, DeferParent, DeferChild }
public sealed record GeneratedHierarchyCommand;
public sealed record GeneratedHierarchyOpen;
public sealed record GeneratedHierarchyNext;

[DaprActor("GeneratedHierarchy")]
public sealed class GeneratedHierarchyActor(ActorActivationContext context, IActorTimerScheduler timers)
    : StateMachineActor<GeneratedHierarchyState, int>(context, timers, "GeneratedHierarchy", 0), IGeneratedHierarchyActor
{
    protected override void Configure(IStateMachine<GeneratedHierarchyState, int> sm)
    {
        sm.InitialState(GeneratedHierarchyState.Waiting);
        sm.In(GeneratedHierarchyState.Waiting).Defer<GeneratedHierarchyCommand>();
        sm.In(GeneratedHierarchyState.Waiting).On<GeneratedHierarchyOpen>().GoTo(GeneratedHierarchyState.IgnoreChild);
        sm.In(GeneratedHierarchyState.IgnoreParent).Ignore<GeneratedHierarchyCommand>();
        sm.In(GeneratedHierarchyState.DeferParent).Defer<GeneratedHierarchyCommand>();
        sm.In(GeneratedHierarchyState.IgnoreChild).SubstateOf(GeneratedHierarchyState.IgnoreParent);
        sm.In(GeneratedHierarchyState.DeferChild).SubstateOf(GeneratedHierarchyState.DeferParent);
        sm.In(GeneratedHierarchyState.IgnoreChild).On<GeneratedHierarchyNext>().GoTo(GeneratedHierarchyState.DeferChild);
        foreach (var child in new[] { GeneratedHierarchyState.IgnoreChild, GeneratedHierarchyState.DeferChild })
            sm.In(child).On<GeneratedHierarchyCommand>().Do(ctx => ctx.Update(value => value + 1)).Reply(7);
    }

    public Task QueueAsync(CancellationToken cancellationToken = default) => Raise<object>(new GeneratedHierarchyCommand(), cancellationToken);
    public Task OpenAsync(CancellationToken cancellationToken = default) => Raise<object>(new GeneratedHierarchyOpen(), cancellationToken);
    public Task NextAsync(CancellationToken cancellationToken = default) => Raise<object>(new GeneratedHierarchyNext(), cancellationToken);
    public Task<int> CommandAsync(CancellationToken cancellationToken = default) => Raise<int>(new GeneratedHierarchyCommand(), cancellationToken);
    public Task<int> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Data);
}
