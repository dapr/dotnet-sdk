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
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.StateMachine.Test;

public sealed class HierarchyTimeoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Parent_timeout_survives_sibling_transition_and_child_timeout(bool childTimeout)
    {
        await using var runtime = new ActorTestRuntime(services =>
        {
            services.AddSingleton(new HierarchyTimeoutOptions(childTimeout));
            services.AddDaprActors(_ => { });
        });
        var id = new ActorId("hierarchy-timeout");
        async Task<T> Invoke<T>(string operation)
        {
            var call = runtime.InvokeAsync("HierarchyTimeout", id, operation);
            await runtime.RunToIdle().WaitAsync(TimeSpan.FromSeconds(5));
            var bytes = await call;
            return bytes is { Length: > 0 }
                ? JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))!
                : default!;
        }

        Assert.Equal(TimeoutState.First, (await Invoke<TimeoutSnapshot>("Read")).State);
        runtime.Time.Advance(TimeSpan.FromSeconds(2));
        await Invoke<object?>("Next");
        runtime.Time.Advance(TimeSpan.FromSeconds(7));
        await runtime.RunToIdle();
        var beforeParent = await Invoke<TimeoutSnapshot>("Read");
        Assert.Equal(TimeoutState.Second, beforeParent.State);
        Assert.Equal(childTimeout ? 1 : 0, beforeParent.ChildTimeouts);
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(TimeoutState.Done, (await Invoke<TimeoutSnapshot>("Read")).State);
    }
}

public sealed record HierarchyTimeoutOptions(bool ChildTimeout);
public enum TimeoutState { Parent, First, Second, Done }
public sealed record TimeoutNext;
public sealed record TimeoutSnapshot(TimeoutState State, int ChildTimeouts);

[GenerateActorClient]
public interface IHierarchyTimeoutActor : IActor
{
    Task<TimeoutSnapshot> Read();
    Task Next();
}

[DaprActor("HierarchyTimeout")]
public sealed class HierarchyTimeoutActor(
    ActorActivationContext context, IActorTimerScheduler timers, HierarchyTimeoutOptions options)
    : StateMachineActor<TimeoutState, int>(context, timers, "HierarchyTimeout", 0), IHierarchyTimeoutActor
{
    protected override void Configure(IStateMachine<TimeoutState, int> machine)
    {
        machine.InitialState(TimeoutState.First);
        machine.In(TimeoutState.Parent).After(TimeSpan.FromSeconds(10));
        machine.In(TimeoutState.Parent).On<StateTimeout<TimeoutState>>()
            .When((_, evt) => evt.State == TimeoutState.Parent).GoTo(TimeoutState.Done)
            .Otherwise().Do(ctx => ctx.Update(count => count + 1));
        machine.In(TimeoutState.First).SubstateOf(TimeoutState.Parent);
        machine.In(TimeoutState.First).On<TimeoutNext>().GoTo(TimeoutState.Second);
        machine.In(TimeoutState.Second).SubstateOf(TimeoutState.Parent);
        if (options.ChildTimeout)
        {
            machine.In(TimeoutState.First).After(TimeSpan.FromSeconds(5));
            machine.In(TimeoutState.Second).After(TimeSpan.FromSeconds(7));
        }
        machine.In(TimeoutState.Done);
    }

    public Task<TimeoutSnapshot> Read() => Task.FromResult(new TimeoutSnapshot(CurrentState, Data));
    public Task Next() => Raise<object?>(new TimeoutNext());
}
