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

using System.Reflection;
using System.Text;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.State.Versioning;
using Dapr.Actors.Next.Interpreted;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Examples.Migration.Tests;

public sealed class MigrationTests
{
    private const string InterpretedActorStateShapeHash = "h1:manual-interpreted-state";

    [Fact]
    public async Task GetOrCreateAsync_folds_seeded_v1_to_current_cart()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("cart-v1");
        await runtime.SeedStateAsync("MigratingCart", id, "cart", new CartStateV1 { Skus = ["sku-1", "sku-1", "sku-2"] });
        var cart = CreateCart(runtime, id);

        var read = cart.GetState();
        await runtime.RunToIdle();
        var current = await read;

        Assert.Equal(2, current.Lines.Single(line => line.Sku == "sku-1").Quantity);
        Assert.Equal(3, current.TotalQuantity);
        Assert.Equal(3, runtime.StateOf(cart).Get<CartStateV3>("cart")!.TotalQuantity);
    }

    [Fact]
    public async Task TryGetAsync_folds_seeded_v2_to_current_cart()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("cart-v2");
        await runtime.SeedStateAsync(
            "MigratingCart",
            id,
            "cart",
            new CartStateV2 { Lines = [new CartLine("sku-3", 2), new CartLine("sku-4", 5)] });
        var cart = CreateCart(runtime, id);

        var read = cart.TryGetState();
        await runtime.RunToIdle();
        var current = await read;

        Assert.NotNull(current);
        Assert.Equal(7, current.TotalQuantity);
        Assert.Equal(7, runtime.StateOf(cart).Get<CartStateV3>("cart")!.TotalQuantity);
    }

    [Fact]
    public async Task Auto_generated_additive_chain_folds_without_authored_upcasters()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("auto");
        await runtime.SeedStateAsync("MigratingCart", id, "autonomous", new MyState { Name = "Ada" });
        var cart = CreateCart(runtime, id);

        var read = cart.GetAutonomousState();
        await runtime.RunToIdle();
        var current = await read;

        Assert.Equal("Ada", current.Name);
        Assert.Equal(0, current.Age);
        Assert.False(current.Active);
        Assert.Equal("Ada", runtime.StateOf(cart).Get<MyStateV3>("autonomous")!.Name);
    }

    [Fact]
    public async Task Hand_authored_non_additive_family_folds()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("renamed");
        await runtime.SeedStateAsync("MigratingCart", id, "renamed", new RenamedState { FirstName = "Ada", LastName = "Lovelace" });
        var cart = CreateCart(runtime, id);

        var read = cart.GetRenamedState();
        await runtime.RunToIdle();
        var current = await read;

        Assert.Equal("Ada Lovelace", current.DisplayName);
        Assert.Equal("Ada Lovelace", runtime.StateOf(cart).Get<RenamedStateV2>("renamed")!.DisplayName);
    }

    [Fact]
    public async Task Corruption_guard_fails_loud_when_seeded_shape_hash_drifts()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("corrupt");
        var envelope = new ActorStateEnvelope<CartStateV1>(
            ActorStateEnvelopeHeader.Create(ActorStateFormKind.Enveloped, GetSerializerId(runtime), GetSerializerVersion(runtime)),
            new ActorStateDiscriminator(0, "h1:mutated"),
            new CartStateV1 { Skus = ["sku-1"] });
        await RawWriteState(runtime, "MigratingCart", id, "cart", envelope);
        var cart = CreateCart(runtime, id);

        var read = cart.GetState();
        await runtime.RunToIdle();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => read);
        Assert.Contains("shape drift", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Families_are_isolated_within_one_actor()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("families");
        await runtime.SeedStateAsync("MigratingCart", id, "cart", new CartStateV1 { Skus = ["cart", "cart"] });
        await runtime.SeedStateAsync("MigratingCart", id, "autonomous", new MyState { Name = "profile" });
        await runtime.SeedStateAsync("MigratingCart", id, "renamed", new RenamedState { FirstName = "Grace", LastName = "Hopper" });
        var cart = CreateCart(runtime, id);

        var cartRead = cart.GetState();
        var autoRead = cart.GetAutonomousState();
        var renamedRead = cart.GetRenamedState();
        await runtime.RunToIdle();

        Assert.Equal(2, (await cartRead).TotalQuantity);
        Assert.Equal("profile", (await autoRead).Name);
        Assert.Equal("Grace Hopper", (await renamedRead).DisplayName);
    }

    [Fact]
    public async Task Migrating_read_repersists_once_at_target_node()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("repersist");
        await runtime.SeedStateAsync("MigratingCart", id, "cart", new CartStateV1 { Skus = ["sku-1"] });
        var cart = CreateCart(runtime, id);

        var first = cart.GetState();
        await runtime.RunToIdle();
        Assert.Equal(1, (await first).TotalQuantity);
        Assert.Equal(1, runtime.StateOf(cart).Get<CartStateV3>("cart")!.TotalQuantity);

        runtime.Faults.FailNextStateWrite<CartStateV3>(stateName: "cart");
        var second = cart.GetState();
        await runtime.RunToIdle();
        Assert.Equal(1, (await second).TotalQuantity);

        var write = cart.AddSku("sku-2");
        await runtime.RunToIdle();
        await Assert.ThrowsAsync<ActorInjectedTransientException>(() => write);
    }

    [Fact]
    public async Task SetAsync_of_legacy_type_imports_then_next_read_folds()
    {
        await using var runtime = CreateRuntime();
        var cart = CreateCart(runtime, ActorId.Create("import"));

        var import = cart.ImportLegacyV1(new CartStateV1 { Skus = ["sku-1", "sku-1", "sku-2"] });
        await runtime.RunToIdle();
        await import;
        Assert.Equal(2, runtime.StateOf(cart).Get<CartStateV1>("cart")!.Skus.Count(sku => sku == "sku-1"));

        var read = cart.GetState();
        await runtime.RunToIdle();
        Assert.Equal(3, (await read).TotalQuantity);
        Assert.Equal(3, runtime.StateOf(cart).Get<CartStateV3>("cart")!.TotalQuantity);
    }

    [Fact]
    public async Task Graduation_writes_plain_and_reimport_can_move_to_new_shape()
    {
        await using var runtime = CreateRuntime();
        var cart = CreateCart(runtime, ActorId.Create("graduated"));

        var import = cart.ImportGraduated(new GraduatedCartState
        {
            Lines = [new CartLine("sku-1", 2)],
            TotalQuantity = 2,
        });
        await runtime.RunToIdle();
        await import;

        var graduate = cart.GraduateCart();
        await runtime.RunToIdle();
        await graduate;
        Assert.Equal(2, runtime.StateOf(cart).Get<GraduatedCartState>("graduated")!.TotalQuantity);

        var reimport = cart.GetReimportedGraduatedState();
        await runtime.RunToIdle();
        var current = await reimport;

        Assert.Equal(2, current.TotalQuantity);
        Assert.Equal("USD", current.Currency);
        Assert.Equal("USD", runtime.StateOf(cart).Get<GraduatedCartStateV2>("graduated")!.Currency);
    }

    [Fact]
    public async Task Fault_during_migration_leaves_seeded_node_and_next_turn_recovers()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("fault");
        await runtime.SeedStateAsync("MigratingCart", id, "cart", new CartStateV1 { Skus = ["sku-1", "sku-2"] });
        runtime.Faults.FailNextUpcastHop<CartStateV1, CartStateV2>(stateName: "cart");
        var cart = CreateCart(runtime, id);

        var failed = cart.GetState();
        await runtime.RunToIdle();
        await Assert.ThrowsAsync<ActorInjectedTransientException>(() => failed);
        Assert.Equal(2, runtime.StateOf(cart).Get<CartStateV1>("cart")!.Skus.Count);

        var recovered = cart.GetState();
        await runtime.RunToIdle();
        Assert.Equal(2, (await recovered).TotalQuantity);
    }

    [Fact]
    public async Task Full_disable_opt_out_stores_plain_legacy_type()
    {
        await using var runtime = CreateRuntime(options => options.DisableStateMigration = true);
        var id = ActorId.Create("disabled");
        var cart = CreateCart(runtime, id);

        var import = cart.ImportLegacyV1(new CartStateV1 { Skus = ["sku-1"] });
        await runtime.RunToIdle();
        await import;

        Assert.Equal("sku-1", runtime.StateOf(cart).Get<CartStateV1>("cart")!.Skus.Single());
        var stored = await RawReadState<ActorStatePlainEnvelope<CartStateV1>>(runtime, "MigratingCart", id, "cart");
        Assert.Equal(ActorStateFormKind.Plain, stored!.Header.FormKind);
    }

    [Fact]
    public async Task Full_disable_still_reads_existing_enveloped_state_and_repersists_plain()
    {
        await using var runtime = CreateRuntime(options => options.DisableStateMigration = true);
        var id = ActorId.Create("disabled-existing");
        await runtime.SeedStateAsync("MigratingCart", id, "cart", new CartStateV1 { Skus = ["sku-1", "sku-1"] });
        var cart = CreateCart(runtime, id);

        var read = cart.GetState();
        await runtime.RunToIdle();
        var current = await read;

        Assert.Equal(2, current.TotalQuantity);
        var stored = await RawReadState<ActorStatePlainEnvelope<CartStateV3>>(runtime, "MigratingCart", id, "cart");
        Assert.Equal(ActorStateFormKind.Plain, stored!.Header.FormKind);
        Assert.Equal(2, stored.Value.TotalQuantity);
    }

    [Fact]
    public async Task Full_disable_graduates_existing_enveloped_state_to_plain()
    {
        await using var runtime = CreateRuntime(options => options.DisableStateMigration = true);
        var id = ActorId.Create("disabled-graduate");
        await runtime.SeedStateAsync(
            "MigratingCart",
            id,
            "graduated",
            new GraduatedCartState { Lines = [new CartLine("sku-1", 2)], TotalQuantity = 2 });
        var cart = CreateCart(runtime, id);

        var graduate = cart.GraduateCart();
        await runtime.RunToIdle();
        await graduate;

        var stored = await RawReadState<ActorStatePlainEnvelope<GraduatedCartState>>(runtime, "MigratingCart", id, "graduated");
        Assert.Equal(ActorStateFormKind.Plain, stored!.Header.FormKind);
        Assert.Equal(2, stored.Value.TotalQuantity);
    }

    [Fact]
    public async Task Interpreted_actor_state_round_trips_outside_typed_migration_path()
    {
        var store = new InMemoryInterpretedMachineStore();
        var registry = new CountingCapabilityRegistry();
        var id = ActorId.Create("machine");
        await new InterpretedMachineDeployer(new InterpretedMachineVerifier(registry), store)
            .DeployAsync("Machine", id, InterpretedDefinition());
        await using var runtime = new ActorTestRuntime(services =>
        {
            services.AddDaprActorStateMigration(InterpretedMigrationFamily());
            services.AddSingleton<IInterpretedMachineStore>(store);
            services.AddSingleton<ICapabilityRegistry>(registry);
            services.AddDaprInterpretedActors("Machine");
        });

        await Raise(runtime, id, 3);
        var deactivate = runtime.InvokeAsync("Machine", id, "deactivate", kind: Dapr.Actors.Next.Abstractions.Scheduling.ActorTurnKind.Deactivate);
        await runtime.RunToIdle();
        await deactivate;
        await Raise(runtime, id, 4);

        var interpreted = runtime.StateOf("Machine", id).Get<InterpretedActorState>("__interpreted");
        Assert.NotNull(interpreted);
        Assert.Equal(1, interpreted.DocumentVersion);
        Assert.Equal(2, interpreted.Data["count"].GetInt32());
        var stored = await RawReadState<ActorStatePlainEnvelope<InterpretedActorState>>(runtime, "Machine", id, "__interpreted");
        Assert.Equal(ActorStateFormKind.Plain, stored!.Header.FormKind);
    }

    private static ActorTestRuntime CreateRuntime(Action<DaprActorsOptions>? configure = null)
    {
        _ = typeof(MigratingCartActor);
        return new ActorTestRuntime(services => services.AddDaprActors(configure ?? (_ => { })));
    }

    private static IMigratingCartActor CreateCart(ActorTestRuntime runtime, ActorId id) =>
        runtime.CreateActor<IMigratingCartActor>(id, "MigratingCart");

    private static async Task<JsonDocument> Raise(ActorTestRuntime runtime, ActorId id, int value)
    {
        var payload = JsonSerializer.Serialize(new InterpretedEvent("add", Json(value)));
        var pending = runtime.InvokeAsync("Machine", id, "Raise", payload);
        await runtime.RunToIdle();
        return JsonDocument.Parse(Encoding.UTF8.GetString((await pending)!));
    }

    private static async ValueTask RawWriteState<T>(ActorTestRuntime runtime, string actorType, ActorId actorId, string name, T value)
    {
        var store = runtime.GetType().GetProperty("StateStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var serializer = runtime.GetType().GetProperty("Serializer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var bytes = (byte[])serializer.GetType().GetMethod("SerializeToBytes")!.MakeGenericMethod(typeof(T)).Invoke(serializer, [value])!;
        var write = store.GetType().GetMethod("WriteAsync")!;
        var result = (ValueTask)write.Invoke(store, [actorType, actorId.Value, name, new ReadOnlyMemory<byte>(bytes), CancellationToken.None])!;
        await result;
    }

    private static async ValueTask<T?> RawReadState<T>(ActorTestRuntime runtime, string actorType, ActorId actorId, string name)
    {
        var store = runtime.GetType().GetProperty("StateStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var serializer = runtime.GetType().GetProperty("Serializer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var read = store.GetType().GetMethod("ReadAsync")!;
        var result = (ValueTask<ReadOnlyMemory<byte>?>)read.Invoke(store, [actorType, actorId.Value, name, CancellationToken.None])!;
        var bytes = await result;
        if (bytes is null)
        {
            return default;
        }

        return (T?)serializer.GetType().GetMethod("DeserializeFromBytes")!.MakeGenericMethod(typeof(T)).Invoke(serializer, [bytes.Value]);
    }

    private static string GetSerializerId(ActorTestRuntime runtime)
    {
        var serializer = runtime.GetType().GetProperty("Serializer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        return (string)serializer.GetType().GetProperty("SerializerId")!.GetValue(serializer)!;
    }

    private static int GetSerializerVersion(ActorTestRuntime runtime)
    {
        var serializer = runtime.GetType().GetProperty("Serializer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        return (int)serializer.GetType().GetProperty("SerializerVersion")!.GetValue(serializer)!;
    }

    private static InterpretedMachineDefinition InterpretedDefinition() =>
        new()
        {
            DocumentVersion = 1,
            InitialState = "Open",
            States = [new InterpretedStateDefinition { Name = "Open" }],
            Transitions =
            [
                new InterpretedTransitionDefinition
                {
                    Source = "Open",
                    Event = "add",
                    Branches =
                    [
                        new InterpretedBranchDefinition
                        {
                            Otherwise = true,
                            Effects = ["increment"],
                        },
                    ],
                },
            ],
        };

    private static JsonElement Json<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<ActorStateMigrationFamilyRegistration> InterpretedMigrationFamily()
    {
        var node = new ActorStateMigrationNode(0, typeof(InterpretedActorState), InterpretedActorStateShapeHash);
        return
        [
            new ActorStateMigrationFamilyRegistration(
                new ActorStateMigrationFamily("InterpretedActorState", [node], []),
                [new ActorStateNodeDeserializer(0, static (payload, serializer) => serializer.DeserializeFromBytes<ActorStateEnvelope<InterpretedActorState>>(payload)?.Value)],
                []),
        ];
    }

    private sealed class CountingCapabilityRegistry : ICapabilityRegistry
    {
        private readonly IncrementEffect effect = new();

        public bool TryGetEffect(string name, out IActorEffect effect)
        {
            effect = this.effect;
            return string.Equals(name, "increment", StringComparison.Ordinal);
        }

        public bool TryGetGuard(string name, out IActorGuard guard)
        {
            guard = null!;
            return false;
        }
    }

    private sealed class IncrementEffect : IActorEffect
    {
        public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
        {
            var state = (DynamicStateBag)context.Arguments["state"]!;
            state.Set("count", state.Get<int>("count") + 1);
            return ValueTask.CompletedTask;
        }
    }
}
