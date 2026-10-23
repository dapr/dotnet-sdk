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
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors.Next;

public sealed class StateAndTimerIntegrationTests
{
    private static readonly ActorId Id = new("state-and-timers");
    private static ActorTestRuntime CreateRuntime(GeneratedReentrantStateProbe? probe = null, bool remote = false) =>
        new(services =>
        {
            services.AddSingleton(probe ?? new GeneratedReentrantStateProbe());
            services.AddDaprActors(options => options.EnableSidecarTransport = false);
        }, new ActorTestRuntimeOptions { SimulateRemoteCalls = remote });

    private static async Task Drain(ActorTestRuntime runtime, Task call)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        do
        {
            timeout.Token.ThrowIfCancellationRequested();
            await runtime.RunToIdle(timeout.Token);
            await Task.Yield();
        } while (!call.IsCompleted);
        await call.WaitAsync(timeout.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generated_reentrant_proxy_preserves_reversion_through_child_and_parent_commits(bool remote)
    {
        var probe = new GeneratedReentrantStateProbe();
        await using var runtime = CreateRuntime(probe, remote);
        var actor = runtime.CreateActor<IGeneratedReentrantStateActor>(Id, "GeneratedReentrantState");
        await Drain(runtime, actor.ReadAsync());
        using var pause = runtime.Faults.PauseNextStateWrite<int>("value");
        probe.Pause = pause;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Drain(runtime, actor.RevertAsync(timeout.Token));
        await Drain(runtime, runtime.DeactivateAsync("GeneratedReentrantState", Id));
        var read = actor.ReadAsync();
        await Drain(runtime, read);
        Assert.Equal(0, await read);
    }

    [Fact]
    public async Task Generated_machine_rejects_queued_timeout_after_reentry_and_accepts_replacement()
    {
        await using var runtime = CreateRuntime();
        var actor = runtime.CreateActor<IGeneratedDeadlineActor>(Id, "GeneratedDeadline");
        await Drain(runtime, actor.ReadAsync());
        var restart = actor.RestartAsync();
        runtime.Time.Advance(TimeSpan.FromMilliseconds(500));
        await Drain(runtime, restart);
        var read = actor.ReadAsync();
        await Drain(runtime, read);
        Assert.Equal(GeneratedDeadlineState.Waiting, (await read).State);
        runtime.Time.Advance(TimeSpan.FromMilliseconds(500));
        await runtime.RunToIdle();
        read = actor.ReadAsync();
        await Drain(runtime, read);
        Assert.Equal(GeneratedDeadlineState.Done, (await read).State);
    }

    [Fact]
    public async Task Generated_parent_and_child_timeouts_keep_independent_deadlines()
    {
        await using var runtime = CreateRuntime();
        var actor = runtime.CreateActor<IGeneratedParentDeadlineActor>(Id, "GeneratedParentDeadline");
        await Drain(runtime, actor.EnterAndMoveAsync());
        runtime.Time.Advance(TimeSpan.FromMilliseconds(100));
        await runtime.RunToIdle();
        var read = actor.ReadAsync();
        await Drain(runtime, read);
        Assert.Equal(new GeneratedParentDeadlineSnapshot(GeneratedParentDeadlineState.Second, 1), await read);
        runtime.Time.Advance(TimeSpan.FromMilliseconds(400));
        await runtime.RunToIdle();
        read = actor.ReadAsync();
        await Drain(runtime, read);
        Assert.Equal(new GeneratedParentDeadlineSnapshot(GeneratedParentDeadlineState.Done, 1), await read);
    }

    [Fact]
    public async Task Deactivated_generated_actor_receives_no_timer_turns()
    {
        await using var runtime = CreateRuntime();
        var actor = runtime.CreateActor<IGeneratedDeadlineActor>(Id, "GeneratedDeadline");
        await Drain(runtime, actor.ReadAsync());
        await Drain(runtime, runtime.DeactivateAsync("GeneratedDeadline", Id));
        var turns = runtime.Transcript.Count;
        runtime.Time.Advance(TimeSpan.FromSeconds(2));
        await runtime.RunToIdle();
        Assert.Equal(turns, runtime.Transcript.Count);
        var read = actor.ReadAsync();
        await Drain(runtime, read);
        Assert.Equal(GeneratedDeadlineState.Waiting, (await read).State);
        Assert.Equal(1, (await read).Entries);
    }
}
