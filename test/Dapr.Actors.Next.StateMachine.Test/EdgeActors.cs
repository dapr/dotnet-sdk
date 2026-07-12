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
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.StateMachine.Test;

public enum EdgeState
{
    A,
    B,
}

public sealed record EdgeData(string[] Log)
{
    public static EdgeData Empty { get; } = new([]);
}

public sealed record Begin;

public sealed record Follow;

public sealed record UseNamedEffect;

public sealed record UseNamedGuard;

public sealed record ScheduleCustomTimer;

public sealed record CancelCustomTimer;

public sealed record UnknownEdgeEvent;

public sealed record NoBranch;

public sealed record NullReply;

public sealed record WrongReply;

public sealed record FallbackData(string Value);

public interface IEdgeActor : IActor;

public interface IMissingInitialActor : IActor;

public interface IFallbackActor : IActor;

public sealed class EdgeActor(ActorActivationContext context, IActorTimerScheduler timerScheduler) :
    StateMachineActor<EdgeState, EdgeData>(context, timerScheduler, "Edge", EdgeData.Empty),
    IEdgeActor
{
    protected override void Configure(IStateMachine<EdgeState, EdgeData> sm)
    {
        sm.InitialState(EdgeState.A);
        sm.In(EdgeState.A)
            .OnEntry("named-entry")
            .OnExit(async ctx =>
            {
                await Task.Yield();
                ctx.Update(data => data with { Log = [.. data.Log, "exit-a"] });
            })
            .On<Begin>()
                .Do(ctx => ctx.Update(data => data with { Log = [.. data.Log, "begin"] }))
                .Raise(new Follow())
                .GoTo(EdgeState.B)
                .Reply("begun");

        sm.In(EdgeState.A)
            .On<UseNamedEffect>().Do("named-effect");

        sm.In(EdgeState.A)
            .On<UseNamedGuard>().When("named-guard").Reply("guarded").Otherwise().Reply("fallback");

        sm.In(EdgeState.A)
            .On<NoBranch>();

        sm.In(EdgeState.A)
            .On<NullReply>().Reply<object?>(null);

        sm.In(EdgeState.A)
            .On<WrongReply>().Reply("wrong");

        sm.In(EdgeState.A)
            .On<ScheduleCustomTimer>()
                .Do(ctx => ctx.Timers.Schedule("custom", TimeSpan.FromSeconds(1)))
                .Reply("scheduled");

        sm.In(EdgeState.A)
            .On<CancelCustomTimer>()
                .Do(ctx =>
                {
                    ctx.Timers.Schedule("custom", TimeSpan.FromSeconds(1));
                    ctx.Timers.Cancel("custom");
                })
                .Reply("canceled");

        sm.In(EdgeState.A)
            .On<StateMachineTimerFired>()
                .Do(ctx => ctx.Update(data => data with { Log = [.. data.Log, $"timer:{ctx.Event.Name}"] }));

        sm.In(EdgeState.B)
            .OnEntry(async ctx =>
            {
                await Task.Yield();
                ctx.Update(data => data with { Log = [.. data.Log, "entry-b"] });
            })
            .On<Follow>()
                .Do(ctx => ctx.Update(data => data with { Log = [.. data.Log, "follow"] }));
    }

    public Task<string> Begin(CancellationToken cancellationToken = default) => Raise<string>(new Begin(), cancellationToken);

    public Task<string> UseNamedEffect(CancellationToken cancellationToken = default) => Raise<string>(new UseNamedEffect(), cancellationToken);

    public Task<string> UseNamedGuard(CancellationToken cancellationToken = default) => Raise<string>(new UseNamedGuard(), cancellationToken);

    public Task<string> ScheduleCustomTimer(CancellationToken cancellationToken = default) => Raise<string>(new ScheduleCustomTimer(), cancellationToken);

    public Task<string> CancelCustomTimer(CancellationToken cancellationToken = default) => Raise<string>(new CancelCustomTimer(), cancellationToken);

    public Task<object?> Unknown(CancellationToken cancellationToken = default) => Raise<object?>(new UnknownEdgeEvent(), cancellationToken);

    public Task<string> NoBranch(CancellationToken cancellationToken = default) => Raise<string>(new NoBranch(), cancellationToken);

    public Task<object?> NullReply(CancellationToken cancellationToken = default) => Raise<object?>(new NullReply(), cancellationToken);

    public Task<int> WrongReply(CancellationToken cancellationToken = default) => Raise<int>(new WrongReply(), cancellationToken);

    public Task<int> TableStateCount(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(GetTransitionTable().States.Count + GetTransitionTable().States.Count);
    }

    public Task<EdgeData> ReadData(CancellationToken cancellationToken = default) => Task.FromResult(Data);
}

public sealed class MissingInitialActor(ActorActivationContext context, IActorTimerScheduler timerScheduler) :
    StateMachineActor<EdgeState, EdgeData>(context, timerScheduler, "MissingInitial", EdgeData.Empty),
    IMissingInitialActor
{
    protected override void Configure(IStateMachine<EdgeState, EdgeData> sm)
    {
        sm.In(EdgeState.A).On<Begin>().Reply("begin");
    }

    public Task<string> Begin(CancellationToken cancellationToken = default) => Raise<string>(new Begin(), cancellationToken);
}

public sealed class FallbackActor(ActorActivationContext context, IActorTimerScheduler timerScheduler) :
    StateMachineActor<EdgeState, FallbackData>(context, timerScheduler, "Fallback", new FallbackData("")),
    IFallbackActor
{
    protected override void Configure(IStateMachine<EdgeState, FallbackData> sm)
    {
        sm.InitialState(EdgeState.A);
        sm.In(EdgeState.A).Ignore<Begin>();
        sm.In(EdgeState.B).Ignore<Begin>();
        sm.OnUnhandled(ctx =>
        {
            ctx.Update(_ => new FallbackData(ctx.Event.GetType().Name));
            ctx.Reply("fallback");
        });
    }

    public Task<string> Unknown(CancellationToken cancellationToken = default) => Raise<string>(new UnknownEdgeEvent(), cancellationToken);
}

public sealed class EdgeActorDispatcher : IActorDispatcher
{
    public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
    {
        if (actor is EdgeActor edge)
        {
            if (request.MethodName == StateMachineConstants.TimerOperationName)
            {
                await edge.DispatchStateMachineTimerAsync(System.Text.Encoding.UTF8.GetString(request.Payload.Span), cancellationToken);
                return new ActorDispatchResponse(null);
            }

            return request.MethodName switch
            {
                "Begin" => Json(await edge.Begin(cancellationToken)),
                "UseNamedEffect" => Json(await edge.UseNamedEffect(cancellationToken)),
                "UseNamedGuard" => Json(await edge.UseNamedGuard(cancellationToken)),
                "ScheduleCustomTimer" => Json(await edge.ScheduleCustomTimer(cancellationToken)),
                "CancelCustomTimer" => Json(await edge.CancelCustomTimer(cancellationToken)),
                "Unknown" => Json(await edge.Unknown(cancellationToken)),
                "NoBranch" => Json(await edge.NoBranch(cancellationToken)),
                "NullReply" => Json(await edge.NullReply(cancellationToken)),
                "WrongReply" => Json(await edge.WrongReply(cancellationToken)),
                "TableStateCount" => Json(await edge.TableStateCount(cancellationToken)),
                "ReadData" => Json(await edge.ReadData(cancellationToken)),
                _ => throw new InvalidOperationException("Unknown edge operation."),
            };
        }

        if (actor is MissingInitialActor missing)
        {
            return Json(await missing.Begin(cancellationToken));
        }

        var fallback = (FallbackActor)actor;
        return Json(await fallback.Unknown(cancellationToken));
    }

    private static ActorDispatchResponse Json<T>(T value) => new(JsonSerializer.SerializeToUtf8Bytes(value));
}
