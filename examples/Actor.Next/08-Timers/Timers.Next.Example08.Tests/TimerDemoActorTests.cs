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
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Testing;

namespace Dapr.Actors.Next.Examples.Timers.Tests;

public sealed class TimerDemoActorTests
{
    [Fact]
    public async Task Timers_can_be_scheduled_read_listed_fired_expired_and_cancelled()
    {
        await using var runtime = CreateRuntime();
        var actor = runtime.CreateActor<ITimerDemoActor>(ActorId.Create("demo"), TimerDemoActor.ActorType);

        await Run(runtime, actor.Schedule(new ScheduleTimerRequest("heartbeat", 1, "beat", PeriodSeconds: 1, TtlSeconds: 2)));
        await Run(runtime, actor.Schedule(new ScheduleTimerRequest("one-shot", 60, "hello once")));

        var heartbeat = (await Run(runtime, actor.Get("heartbeat"))).Timer;
        Assert.NotNull(heartbeat);
        Assert.Equal(nameof(ITimerDemoActor.Tick), heartbeat.OperationName);
        Assert.Equal(TimeSpan.FromSeconds(1), heartbeat.DueTime);
        Assert.Equal(TimeSpan.FromSeconds(1), heartbeat.Period);
        Assert.Equal(runtime.Time.GetUtcNow() + TimeSpan.FromSeconds(2), heartbeat.ExpiresAt);
        Assert.Contains("beat", heartbeat.ArgumentsJson);
        Assert.Null((await Run(runtime, actor.Get("missing"))).Timer);
        Assert.Equal(["heartbeat", "one-shot"], (await Run(runtime, actor.List())).Select(timer => timer.Name).Order());

        runtime.Time.Advance(TimeSpan.FromSeconds(3));
        await runtime.RunToIdle();

        Assert.Equal(["heartbeat: beat", "heartbeat: beat"], await Run(runtime, actor.GetFired()));
        Assert.Null((await Run(runtime, actor.Get("heartbeat"))).Timer);
        Assert.Equal(["one-shot"], (await Run(runtime, actor.List())).Select(timer => timer.Name));

        await Run(runtime, actor.Cancel("one-shot"));
        Assert.Empty(await Run(runtime, actor.List()));
    }

    private static async Task<T> Run<T>(ActorTestRuntime runtime, Task<T> call)
    {
        await runtime.RunToIdle();
        return await call;
    }

    private static async Task Run(ActorTestRuntime runtime, Task call)
    {
        await runtime.RunToIdle();
        await call;
    }

    private static ActorTestRuntime CreateRuntime()
    {
        _ = typeof(TimerDemoActor);
        return new ActorTestRuntime(services => services.AddDaprActors(_ => { }));
    }
}
