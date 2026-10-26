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

public sealed class HierarchyDispositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nearest_handler_overrides_parent_ignore_or_defer_and_replays_deferred_events(bool defer)
    {
        await using var runtime = new ActorTestRuntime(services =>
        {
            services.AddSingleton(new HierarchyOptions(defer));
            services.AddDaprActors(_ => { });
        });
        var id = new ActorId("hierarchy");
        async Task<T> Invoke<T>(string operation)
        {
            var call = runtime.InvokeAsync("HierarchyDisposition", id, operation);
            await runtime.RunToIdle();
            var bytes = await call;
            return bytes is { Length: > 0 } ? JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))! : default!;
        }

        // Waiting explicitly defers; its parent handler must not run.
        await Invoke<object?>("Command");
        Assert.Equal(0, await Invoke<int>("Read"));
        await Invoke<object?>("Open");
        Assert.Equal(1, await Invoke<int>("Read"));
        Assert.Equal(7, await Invoke<int>("Command"));
        Assert.Equal(2, await Invoke<int>("Read"));
        await Invoke<object?>("Close");
        // Closed ignores; neither its parent handler nor deferred replay may run.
        await Invoke<object?>("Command");
        Assert.Equal(2, await Invoke<int>("Read"));
    }
}

public sealed record HierarchyOptions(bool Defer);
public enum HierarchyState { Root, Parent, Waiting, Open, Closed }
public sealed record HierarchyCommand;
public sealed record HierarchyOpen;
public sealed record HierarchyClose;

[GenerateActorClient]
public interface IHierarchyActor : IActor
{
    Task<object> Command();
    Task Open();
    Task Close();
    Task<int> Read();
}

[DaprActor("HierarchyDisposition")]
public sealed class HierarchyActor(ActorActivationContext context, IActorTimerScheduler timers, HierarchyOptions options)
    : StateMachineActor<HierarchyState, int>(context, timers, "HierarchyDisposition", 0), IHierarchyActor
{
    protected override void Configure(IStateMachine<HierarchyState, int> sm)
    {
        sm.InitialState(HierarchyState.Waiting);
        sm.In(HierarchyState.Root).On<HierarchyCommand>().Do(ctx => ctx.Update(value => value + 100)).Reply(100);
        sm.In(HierarchyState.Parent).SubstateOf(HierarchyState.Root);
        if (options.Defer) sm.In(HierarchyState.Parent).Defer<HierarchyCommand>();
        else sm.In(HierarchyState.Parent).Ignore<HierarchyCommand>();
        sm.In(HierarchyState.Waiting).SubstateOf(HierarchyState.Root).Defer<HierarchyCommand>();
        sm.In(HierarchyState.Waiting).On<HierarchyOpen>().GoTo(HierarchyState.Open);
        sm.In(HierarchyState.Open).SubstateOf(HierarchyState.Parent);
        sm.In(HierarchyState.Open).On<HierarchyCommand>().Do(ctx => ctx.Update(value => value + 1)).Reply(7);
        sm.In(HierarchyState.Open).On<HierarchyClose>().GoTo(HierarchyState.Closed);
        sm.In(HierarchyState.Closed).SubstateOf(HierarchyState.Root).Ignore<HierarchyCommand>();
    }

    public Task<object> Command() => Raise<object>(new HierarchyCommand());
    public Task Open() => Raise<object?>(new HierarchyOpen());
    public Task Close() => Raise<object?>(new HierarchyClose());
    public Task<int> Read() => Task.FromResult(Data);
}
