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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Interpreted;
using Google.Protobuf;
using P = Dapr.Client.Autogen.Grpc.v1;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.SourceGenerators.OtherSample;
using Dapr.Actors.Next.SourceGenerators.Sample;
using Dapr.Testcontainers.Xunit.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using First = Dapr.Actors.Next.SourceGenerators.OtherSample.First;
using Second = Dapr.Actors.Next.SourceGenerators.OtherSample.Second;

namespace Dapr.IntegrationTest.Actors.Next;

public sealed partial class RealSidecarActorTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Generated_machine_discards_failed_events_and_reenters_external_self_transition()
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var actor = app!.Services.GetRequiredService<IActorProxyFactory>()
            .Create<IGeneratedDeadlineActor>(new ActorId("generated-self"), "GeneratedDeadline");
        await WaitUntilAsync(async () => (await actor.ReadAsync(timeout.Token)).State == GeneratedDeadlineState.Done,
            timeout.Token, () => "Initial timeout did not fire.");
        await Assert.ThrowsAnyAsync<Exception>(() => actor.FailAsync(timeout.Token));
        await actor.CommandAsync(timeout.Token);
        Assert.Equal(GeneratedDeadlineState.Done, (await actor.ReadAsync(timeout.Token)).State);
        Assert.Equal(1, (await actor.ReadAsync(timeout.Token)).Entries);
        await actor.RestartAsync(timeout.Token);
        await actor.RestartAsync(timeout.Token);
        Assert.Equal(3, (await actor.ReadAsync(timeout.Token)).Entries);
        await WaitUntilAsync(async () => (await actor.ReadAsync(timeout.Token)).State == GeneratedDeadlineState.Done,
            timeout.Token, () => "Restarted timeout did not fire.");
    }

    private static partial void ConfigureGeneratedRegressionActors(WebApplicationBuilder builder)
    {
        RuntimeHelpers.RunModuleConstructor(typeof(CalculatorActor).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(First.AccountActor).Module.ModuleHandle);
        builder.Services.AddSingleton<CalculatorDependency>();
        builder.Services.AddSingleton<GeneratedActivationProbe>();
        builder.Services.AddSingleton<IInterpretedMachineStore, InMemoryInterpretedMachineStore>();
        builder.Services.AddSingleton<ICapabilityRegistry>(new EmptyCapabilityRegistry());
        builder.Services.AddDaprInterpretedActors("RegressionMachine");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Interpreted_machine_retries_failed_verification_and_sidecar_state_read()
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var services = app!.Services;
        var definitions = services.GetRequiredService<IInterpretedMachineStore>();
        var id = new ActorId("interpreted-retry");
        InterpretedMachineDefinition Definition(string initial) => new()
        {
            InitialState = initial,
            States = [new InterpretedStateDefinition { Name = "Ready", Terminal = true }],
            Transitions = [new InterpretedTransitionDefinition
            {
                Source = "Ready", Event = "tick",
                Branches = [new InterpretedBranchDefinition { Otherwise = true }],
            }],
        };
        Task<P.InvokeActorResponse> Raise() => client!.InvokeActorAsync(new P.InvokeActorRequest
        {
            ActorType = "RegressionMachine",
            ActorId = id.Value,
            Method = "Raise",
            Data = ByteString.CopyFromUtf8("""{"Name":"tick","Payload":0}"""),
        }, cancellationToken: timeout.Token).ResponseAsync;
        await definitions.SetAsync("RegressionMachine", id, Definition("Missing"), timeout.Token);
        await Assert.ThrowsAnyAsync<Exception>(Raise);
        await Assert.ThrowsAnyAsync<Exception>(Raise);
        await definitions.SetAsync("RegressionMachine", id, Definition("Ready"), timeout.Token);
        services.GetRequiredService<RecordingActorStateStore>().FailNextInterpretedRead = true;
        await Assert.ThrowsAnyAsync<Exception>(Raise);
        var result = await Raise();
        using var json = System.Text.Json.JsonDocument.Parse(result.Data.Memory);
        Assert.Equal("Ready", json.RootElement.GetProperty("State").GetString());
    }

    private sealed class EmptyCapabilityRegistry : ICapabilityRegistry
    {
        public bool TryGetEffect(string name, out IActorEffect effect) { effect = null!; return false; }
        public bool TryGetGuard(string name, out IActorGuard guard) { guard = null!; return false; }
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Generated_contracts_from_multiple_assemblies_round_trip_through_sidecar()
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var proxies = app!.Services.GetRequiredService<IActorProxyFactory>();
        var calculator = proxies.Create<ICalculatorActor>(new ActorId("generated-calculator"), "Calculator");
        Assert.Equal(8, await calculator.SumAsync(3, 4, timeout.Token));
        Assert.Equal(new[] { new ActorId("one"), new ActorId("two") },
            await calculator.EchoAsync(new List<ActorId> { new("one"), new("two") }, timeout.Token));
        Assert.Equal(new[] { 17 }, (await calculator.MapAsync("items", 17, timeout.Token))["items"]);

        var second = proxies.Create<Second.IAccount>(new ActorId("generated-second"), "SecondAccount");
        Assert.Equal("a|b|c|d|e|f", await second.EchoAsync("a", "b", "c", "d", "e", "f", timeout.Token));
        var registry = app.Services.GetRequiredService<IActorRegistry>();
        Assert.True(registry.TryGet("Calculator", out _));
        Assert.True(registry.TryGet("FirstAccount", out _));
        Assert.True(registry.TryGet("SecondAccount", out _));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Generated_inherited_migration_preserves_dirty_state_and_survives_reactivation()
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var services = app!.Services;
        var serializer = services.GetRequiredService<IActorWireSerializer>();
        var migrator = services.GetRequiredService<IActorStateMigrator>();
        Assert.NotNull(migrator.ResolveFamily(typeof(CalculatorStateV2)));
        var node = migrator.ResolveTargetNode(typeof(AccountV1))!;
        var bytes = await migrator.WriteEnvelopedAsync(new AccountV1
        {
            Balance = 42,
            Owner = "owner",
            Count = 3,
            Revision = 9,
        }, node, serializer, timeout.Token);
        var store = services.GetRequiredService<IActorStateStore>();
        var id = new ActorId("generated-migration");
        await store.WriteAsync("FirstAccount", id.Value, "account", bytes, timeout.Token);
        var actor = services.GetRequiredService<IActorProxyFactory>().Create<First.IAccount>(id, "FirstAccount");

        var migrated = await actor.UpdateAndMigrateAsync(99, timeout.Token);
        Assert.Equal(99, migrated.Balance);
        Assert.Equal("owner", migrated.Owner);
        Assert.Equal(9, migrated.Revision);
        Assert.Equal(3, migrated.Count);
        var persisted = await store.ReadAsync("FirstAccount", id.Value, "account", timeout.Token);
        var envelope = serializer.DeserializeFromBytes<ActorStateEnvelope<AccountV2>>(persisted!.Value)!;
        Assert.Equal(migrator.ResolveTargetNode(typeof(AccountV2))!.Index, envelope.Discriminator.ChainIndex);
        Assert.Equal(99, envelope.Value.Balance);

        await services.GetRequiredService<IActorRuntime>().DeactivateAsync("FirstAccount", id, timeout.Token);
        var restored = await actor.ReadAsync(timeout.Token);
        Assert.Equal(99, restored.Balance);
        Assert.Equal("owner", restored.Owner);
        Assert.Equal(9, restored.Revision);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Legacy_object_envelope_is_unwrapped_before_sidecar_state_is_rewritten()
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var services = app!.Services;
        var store = services.GetRequiredService<IActorStateStore>();
        var id = new ActorId("generated-legacy");
        var legacy =
            Encoding.UTF8.GetBytes(
                """{"schemaVersion":1,"value":{"balance":42,"owner":"legacy","count":3,"revision":7}}""");
        await store.WriteAsync("FirstAccount", id.Value, "account", legacy, timeout.Token);
        var actor = services.GetRequiredService<IActorProxyFactory>().Create<First.IAccount>(id, "FirstAccount");
        var restored = await actor.ReadAsync(timeout.Token);
        Assert.Equal(42, restored.Balance);
        Assert.Equal("legacy", restored.Owner);
        Assert.Equal(3, restored.Count);
        Assert.Equal(7, restored.Revision);

        await services.GetRequiredService<IActorRuntime>().DeactivateAsync("FirstAccount", id, timeout.Token);
        Assert.Equal(42, (await actor.ReadAsync(timeout.Token)).Balance);
        var bytes = await store.ReadAsync("FirstAccount", id.Value, "account", timeout.Token);
        var plain = services.GetRequiredService<IActorWireSerializer>()
            .DeserializeFromBytes<ActorStatePlainEnvelope<AccountV2>>(bytes!.Value)!;
        Assert.Equal(42, plain.Value.Balance);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Generated_initial_state_entry_and_reserved_timer_execute_through_sidecar()
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var services = app!.Services;
        var id = new ActorId("generated-deadline");
        var actor = services.GetRequiredService<IActorProxyFactory>()
            .Create<IGeneratedDeadlineActor>(id, "GeneratedDeadline");
        await WaitUntilAsync(async () => (await actor.ReadAsync(timeout.Token)).State == GeneratedDeadlineState.Done,
            timeout.Token, () => "The generated initial-state timeout did not transition the actor.");
        Assert.Equal(1, (await actor.ReadAsync(timeout.Token)).Entries);
        await services.GetRequiredService<IActorRuntime>().DeactivateAsync("GeneratedDeadline", id, timeout.Token);
        var restored = await actor.ReadAsync(timeout.Token);
        Assert.Equal(GeneratedDeadlineState.Done, restored.State);
        Assert.Equal(1, restored.Entries);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Generated_actor_retries_failed_activation_without_reusing_partial_instance()
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var services = app!.Services;
        var actor = services.GetRequiredService<IActorProxyFactory>()
            .Create<IGeneratedActivationActor>(new ActorId("generated-activation"), "GeneratedActivation");
        await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => actor.ReadAsync(timeout.Token));
        Assert.Equal(2, await actor.ReadAsync(timeout.Token));
        var probe = services.GetRequiredService<GeneratedActivationProbe>();
        Assert.Equal(2, probe.Attempts);
        Assert.Equal(1, probe.Disposals);
        await services.GetRequiredService<IActorRuntime>().DeactivateAsync("GeneratedActivation",
            new ActorId("generated-activation"), timeout.Token);
        Assert.Equal(2, probe.Disposals);
    }
}
