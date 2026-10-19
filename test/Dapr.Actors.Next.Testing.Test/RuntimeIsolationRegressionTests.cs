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

using System.Text;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.SourceGenerators.Sample;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Testing.Test;

[CollectionDefinition("ActorProxy facade", DisableParallelization = true)]
public sealed class ActorProxyFacadeCollection;

[Collection("ActorProxy facade")]
public sealed class RuntimeIsolationRegressionTests
{
    [Fact]
    public async Task Overlapping_generated_runtimes_isolate_schedulers_state_faults_and_disposal()
    {
        await using var first = CreateRuntime();
        await using var second = CreateRuntime();
        var id = ActorId.Create("same-id");
        var firstActor = first.CreateActor<ICalculatorActor>(id, "Calculator");
        var secondActor = second.CreateActor<ICalculatorActor>(id, "Calculator");
        first.Faults.FailNextStateWrite<CalculatorState>();

        var firstCall = firstActor.AddAsync(new CalculationInput(4));
        var secondCall = secondActor.AddAsync(new CalculationInput(9));
        await second.RunToIdle();
        await secondCall;
        Assert.False(firstCall.IsCompleted);
        Assert.Empty(first.Transcript);
        Assert.Null(first.StateOf(firstActor).Get<CalculatorState>("calculator"));
        Assert.Equal(10, second.StateOf(secondActor).Get<CalculatorState>("calculator")!.Value);
        await first.RunToIdle();
        await Assert.ThrowsAsync<ActorInjectedTransientException>(() => firstCall);
        Assert.Null(first.StateOf(firstActor).Get<CalculatorState>("calculator"));

        var recover = firstActor.GetAsync().AsTask();
        await first.RunToIdle();
        Assert.Equal(5, (await recover).Value);
        Assert.Equal(5, first.StateOf(firstActor).Get<CalculatorState>("calculator")!.Value);
        Assert.Equal(10, second.StateOf(secondActor).Get<CalculatorState>("calculator")!.Value);
        Assert.Throws<InvalidOperationException>(() => first.StateOf(secondActor));

        await first.DisposeAsync();
        var afterDisposal = second.CreateActor<ICalculatorActor>(id, "Calculator");
        var read = afterDisposal.GetAsync().AsTask();
        await second.RunToIdle();
        Assert.Equal(10, (await read).Value);
        Assert.Equal(10, second.StateOf(afterDisposal).Get<CalculatorState>("calculator")!.Value);
    }

    [Fact]
    public async Task Runtime_neither_configures_nor_resets_process_wide_proxy_factory()
    {
        var sentinel = new SentinelFactory();
        ActorProxy.Configure(sentinel);
        try
        {
            var id = ActorId.Create("sentinel");
            await using (var runtime = CreateRuntime())
            {
                Assert.Same(sentinel.Actor, ActorProxy.Create<IActor>(id, "Ignored"));
                Assert.IsAssignableFrom<ICalculatorActor>(runtime.CreateActor<ICalculatorActor>(id, "Calculator"));
            }

            Assert.Same(sentinel.Actor, ActorProxy.Create<IActor>(id, "Ignored"));
        }
        finally
        {
            ActorProxy.Reset();
        }
    }

    [Fact]
    public async Task Missing_local_factory_does_not_fall_back_to_another_runtime_or_static_factory()
    {
        await using var other = CreateRuntime();
        ActorProxy.Configure(new SentinelFactory());
        try
        {
            await using var runtime = new ActorTestRuntime(services => services.AddDaprActorsCore(registrations =>
                registrations.Add("Plain", typeof(IActor), typeof(SentinelActor),
                    (_, _) => new SentinelActor(), new SentinelDispatcher())));
            var error = Assert.Throws<InvalidOperationException>(() =>
                runtime.CreateActor<IActor>(ActorId.Create("one"), "Calculator"));
            Assert.Contains("No IActorProxyFactory is registered in this ActorTestRuntime", error.Message);
            var weakCall = runtime.InvokeAsync("Plain", ActorId.Create("one"), "Read");
            await runtime.RunToIdle();
            Assert.Equal("42", Encoding.UTF8.GetString((await weakCall)!));
            var actor = other.CreateActor<ICalculatorActor>(ActorId.Create("one"), "Calculator");
            var call = actor.SumAsync(1, 2);
            await other.RunToIdle();
            Assert.Equal(4, await call);
        }
        finally
        {
            ActorProxy.Reset();
        }
    }

    [Fact]
    public async Task State_introspection_reads_legacy_raw_enveloped_and_null_values_and_reports_errors()
    {
        IActorStateStore store = null!;
        await using var runtime = new ActorTestRuntime(services =>
        {
            store = (IActorStateStore)services.Last(descriptor => descriptor.ServiceType == typeof(IActorStateStore)).ImplementationInstance!;
            services.AddDaprActorsCore(_ => { });
        });
        var id = ActorId.Create("legacy");
        var snapshot = runtime.StateOf("Actor", id);
        foreach (var json in new[] { """{"value":42}""", """{"schemaVersion":1,"value":{"value":42}}""",
                     """{"SchemaVersion":1,"Value":{"Value":42}}""" })
        {
            await store.WriteAsync("Actor", id.Value, "state", Encoding.UTF8.GetBytes(json));
            Assert.Equal(42, snapshot.Get<CalculatorState>()!.Value);
        }
        foreach (var json in new[] { "42", """{"schemaVersion":1,"value":42}""" })
        {
            await store.WriteAsync("Actor", id.Value, "state", Encoding.UTF8.GetBytes(json));
            Assert.Equal(42, snapshot.Get<int>());
        }
        foreach (var json in new[] { "null", """{"schemaVersion":1,"value":null}""" })
        {
            await store.WriteAsync("Actor", id.Value, "state", Encoding.UTF8.GetBytes(json));
            Assert.Null(snapshot.Get<CalculatorState>());
            Assert.Null(snapshot.Get<int?>());
        }
        await runtime.SeedStateAsync<CalculatorState?>("Actor", id, "state", null, ActorStateSeedForm.Plain);
        Assert.Null(snapshot.Get<CalculatorState>());
        await runtime.SeedStateAsync<int?>("Actor", id, "state", null, ActorStateSeedForm.Plain);
        Assert.Null(snapshot.Get<int?>());
        foreach (var json in new[] { "{", """{"schemaVersion":1,"value":"bad"}""", """{"schemaVersion":"bad","value":42}""" })
        {
            await store.WriteAsync("Actor", id.Value, "state", Encoding.UTF8.GetBytes(json));
            Assert.ThrowsAny<JsonException>(() => snapshot.Get<int>());
        }
    }

    private static ActorTestRuntime CreateRuntime() => new(services =>
    {
        services.AddSingleton<CalculatorDependency>();
        services.AddDaprActors(_ => { });
    });

    private sealed class SentinelActor : IActor;
    private sealed class SentinelDispatcher : IActorDispatcher
    {
        public ValueTask<ActorDispatchResponse> DispatchAsync(
            IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ActorDispatchResponse(Encoding.UTF8.GetBytes("42")));
    }

    private sealed class SentinelFactory : IActorProxyFactory
    {
        public IActor Actor { get; } = new SentinelActor();
        public TActor Create<TActor>(ActorId actorId, string actorType) where TActor : IActor => (TActor)Actor;
    }
}
