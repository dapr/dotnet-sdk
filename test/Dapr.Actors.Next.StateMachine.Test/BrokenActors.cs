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
