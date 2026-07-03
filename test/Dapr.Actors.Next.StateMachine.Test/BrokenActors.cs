using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.StateMachine.Test;

public enum BrokenState
{
    A,
    B,
    C,
}

public sealed record BrokenData(int Value);

public sealed record BrokenEvent(int Value);

public sealed record CycleData(int Value);

public interface IBrokenActor : IActor;

public sealed class BrokenActor(ActorActivationContext context, IActorTimerScheduler timerScheduler) :
    StateMachineActor<BrokenState, BrokenData>(context, timerScheduler, "Broken", new BrokenData(0)),
    IBrokenActor
{
    protected override void Configure(IStateMachine<BrokenState, BrokenData> sm)
    {
        sm.InitialState(BrokenState.A);
        sm.In(BrokenState.A)
            .On<BrokenEvent>()
                .When((_, evt) => evt.Value > 0)
                    .GoTo(BrokenState.B);
        sm.In(BrokenState.B);
        sm.In(BrokenState.C);
    }
}

public sealed class CycleActor(ActorActivationContext context, IActorTimerScheduler timerScheduler) :
    StateMachineActor<BrokenState, CycleData>(context, timerScheduler, "Cycle", new CycleData(0)),
    IBrokenActor
{
    protected override void Configure(IStateMachine<BrokenState, CycleData> sm)
    {
        sm.InitialState(BrokenState.A);
        sm.In(BrokenState.A).SubstateOf(BrokenState.B).Ignore<BrokenEvent>();
        sm.In(BrokenState.B).SubstateOf(BrokenState.A).Ignore<BrokenEvent>();
        sm.In(BrokenState.C).Ignore<BrokenEvent>();
    }
}
