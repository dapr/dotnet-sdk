namespace Cart.Next.FSharp.Example02.Tests

#nowarn "FS3261" "FS3264" "FS3265"

open System
open System.Collections.Generic
open System.Linq
open System.Reflection
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Exceptions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Abstractions.Options
open Dapr.Actors.Next.Abstractions.Scheduling
open Dapr.Actors.Next.Abstractions.State
open Dapr.Actors.Next.Abstractions.State.Versioning
open Dapr.Actors.Next.Core.DependencyInjection
open Dapr.Actors.Next.Core.State.Versioning
open Dapr.Actors.Next.Interpreted
open Dapr.Actors.Next.Testing
open Microsoft.Extensions.DependencyInjection
open Cart.Next.FSharp.Example02
open Xunit

type MigrationTests() =

    static let InterpretedActorStateShapeHash = "h1:manual-interpreted-state"

    static member private CreateRuntime(configure: Action<DaprActorsOptions>) =
        let gluePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Cart.Next.FSharp.Example02.Glue.dll")
        let glueAssembly = Assembly.LoadFrom(gluePath)
        match glueAssembly.GetType("Dapr.Actors.Next.Generated.GeneratedActorRegistrationModule") with
        | null -> failwith "GeneratedActorRegistrationModule not found in Glue assembly"
        | moduleType ->
            match moduleType.GetMethod("Register", BindingFlags.Static ||| BindingFlags.NonPublic) with
            | null -> failwith "Register method not found"
            | registerMethod -> registerMethod.Invoke(null, null) |> ignore
        let _ = typeof<MigratingCartActor>
        ActorTestRuntime(fun services ->
            services.AddDaprActors(configure) |> ignore)

    static member private CreateRuntime() =
        MigrationTests.CreateRuntime(Action<DaprActorsOptions>(fun _ -> ()))

    static member private CreateCart(runtime: ActorTestRuntime, id: ActorId) =
        runtime.CreateActor<IMigratingCartActor>(id, "MigratingCart")

    [<Fact>]
    member this.GetOrCreateAsync_folds_seeded_v1_to_current_cart() = task {
        use runtime = MigrationTests.CreateRuntime()
        let id = ActorId.Create("cart-v1")
        let v1 = CartStateV1()
        v1.Skus <- ResizeArray(["sku-1"; "sku-1"; "sku-2"])
        do! runtime.SeedStateAsync("MigratingCart", id, "cart", v1)
        let cart = MigrationTests.CreateCart(runtime, id)

        let read = cart.GetState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! current = read

        Assert.Equal(2, current.Lines.Find(fun line -> line.Sku = "sku-1").Quantity)
        Assert.Equal(3, current.TotalQuantity)
        Assert.Equal(3, runtime.StateOf(cart).Get<CartStateV3>("cart").TotalQuantity)
    }

    [<Fact>]
    member this.TryGetAsync_folds_seeded_v2_to_current_cart() = task {
        use runtime = MigrationTests.CreateRuntime()
        let id = ActorId.Create("cart-v2")
        let v2 = CartStateV2()
        v2.Lines <- ResizeArray([{ Sku = "sku-3"; Quantity = 2 }; { Sku = "sku-4"; Quantity = 5 }])
        do! runtime.SeedStateAsync("MigratingCart", id, "cart", v2)
        let cart = MigrationTests.CreateCart(runtime, id)

        let read = cart.TryGetState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! current = read

        Assert.Equal(7, current.TotalQuantity)
        Assert.Equal(7, runtime.StateOf(cart).Get<CartStateV3>("cart").TotalQuantity)
    }

    [<Fact>]
    member this.Additive_chain_folds_to_current_state() = task {
        use runtime = MigrationTests.CreateRuntime()
        let id = ActorId.Create("auto")
        let s = MyState()
        s.Name <- "Ada"
        do! runtime.SeedStateAsync("MigratingCart", id, "autonomous", s)
        let cart = MigrationTests.CreateCart(runtime, id)

        let read = cart.GetAutonomousState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! current = read

        Assert.Equal("Ada", current.Name)
        Assert.Equal(0, current.Age)
        Assert.False(current.Active)
        Assert.Equal("Ada", runtime.StateOf(cart).Get<MyStateV3>("autonomous").Name)
    }

    [<Fact>]
    member this.Hand_authored_non_additive_family_folds() = task {
        use runtime = MigrationTests.CreateRuntime()
        let id = ActorId.Create("renamed")
        let s = RenamedState()
        s.FirstName <- "Ada"
        s.LastName <- "Lovelace"
        do! runtime.SeedStateAsync("MigratingCart", id, "renamed", s)
        let cart = MigrationTests.CreateCart(runtime, id)

        let read = cart.GetRenamedState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! current = read

        Assert.Equal("Ada Lovelace", current.DisplayName)
        Assert.Equal("Ada Lovelace", runtime.StateOf(cart).Get<RenamedStateV2>("renamed").DisplayName)
    }

    [<Fact>]
    member this.Corruption_guard_fails_loud_when_seeded_shape_hash_drifts() = task {
        use runtime = MigrationTests.CreateRuntime()
        let id = ActorId.Create("corrupt")
        let v1 = CartStateV1()
        v1.Skus <- ResizeArray(["sku-1"])
        let envelope =
            ActorStateEnvelope<CartStateV1>(
                ActorStateEnvelopeHeader.Create(ActorStateFormKind.Enveloped, MigrationTests.GetSerializerId(runtime), MigrationTests.GetSerializerVersion(runtime)),
                ActorStateDiscriminator(0, "h1:mutated"),
                v1)
        do! MigrationTests.RawWriteState<ActorStateEnvelope<CartStateV1>>(runtime, "MigratingCart", id, "cart", envelope)
        let cart = MigrationTests.CreateCart(runtime, id)

        let read = cart.GetState(CancellationToken.None)
        do! runtime.RunToIdle()

        let! ex = Assert.ThrowsAsync<ActorStateMigrationException>(Func<Task>(fun () -> read :> Task))
        Assert.Contains("shape drift", ex.Message, StringComparison.OrdinalIgnoreCase)
    }

    [<Fact>]
    member this.Families_are_isolated_within_one_actor() = task {
        use runtime = MigrationTests.CreateRuntime()
        let id = ActorId.Create("families")
        let cart1 = CartStateV1()
        cart1.Skus <- ResizeArray(["cart"; "cart"])
        do! runtime.SeedStateAsync("MigratingCart", id, "cart", cart1)
        let auto = MyState()
        auto.Name <- "profile"
        do! runtime.SeedStateAsync("MigratingCart", id, "autonomous", auto)
        let renamed = RenamedState()
        renamed.FirstName <- "Grace"
        renamed.LastName <- "Hopper"
        do! runtime.SeedStateAsync("MigratingCart", id, "renamed", renamed)
        let cart = MigrationTests.CreateCart(runtime, id)

        let cartRead = cart.GetState(CancellationToken.None)
        let autoRead = cart.GetAutonomousState(CancellationToken.None)
        let renamedRead = cart.GetRenamedState(CancellationToken.None)
        do! runtime.RunToIdle()

        let! cartResult = cartRead
        let! autoResult = autoRead
        let! renamedResult = renamedRead

        Assert.Equal(2, cartResult.TotalQuantity)
        Assert.Equal("profile", autoResult.Name)
        Assert.Equal("Grace Hopper", renamedResult.DisplayName)
    }

    [<Fact>]
    member this.Migrating_read_repersists_once_at_target_node() = task {
        use runtime = MigrationTests.CreateRuntime()
        let id = ActorId.Create("repersist")
        let v1 = CartStateV1()
        v1.Skus <- ResizeArray(["sku-1"])
        do! runtime.SeedStateAsync("MigratingCart", id, "cart", v1)
        let cart = MigrationTests.CreateCart(runtime, id)

        let first = cart.GetState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! firstResult = first
        Assert.Equal(1, firstResult.TotalQuantity)
        Assert.Equal(1, runtime.StateOf(cart).Get<CartStateV3>("cart").TotalQuantity)

        runtime.Faults.FailNextStateWrite<CartStateV3>(stateName = "cart")
        let second = cart.GetState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! secondResult = second
        Assert.Equal(1, secondResult.TotalQuantity)

        let write = cart.AddSku("sku-2", CancellationToken.None)
        do! runtime.RunToIdle()
        let! _ = Assert.ThrowsAsync<ActorInjectedTransientException>(Func<Task>(fun () -> write))
        ()
    }

    [<Fact>]
    member this.SetAsync_of_legacy_type_imports_then_next_read_folds() = task {
        use runtime = MigrationTests.CreateRuntime()
        let cart = MigrationTests.CreateCart(runtime, ActorId.Create("import"))

        let v1 = CartStateV1()
        v1.Skus <- ResizeArray(["sku-1"; "sku-1"; "sku-2"])
        let import = cart.ImportLegacyV1(v1, CancellationToken.None)
        do! runtime.RunToIdle()
        do! import
        let skuCount = runtime.StateOf(cart).Get<CartStateV1>("cart").Skus |> Seq.filter (fun sku -> sku = "sku-1") |> Seq.length
        Assert.Equal(2, skuCount)

        let read = cart.GetState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! result = read
        Assert.Equal(3, result.TotalQuantity)
        Assert.Equal(3, runtime.StateOf(cart).Get<CartStateV3>("cart").TotalQuantity)
    }

    [<Fact>]
    member this.Graduation_writes_plain_and_reimport_can_move_to_new_shape() = task {
        use runtime = MigrationTests.CreateRuntime()
        let cart = MigrationTests.CreateCart(runtime, ActorId.Create("graduated"))

        let g = GraduatedCartState()
        g.Lines <- ResizeArray([{ Sku = "sku-1"; Quantity = 2 }])
        g.TotalQuantity <- 2
        let import = cart.ImportGraduated(g, CancellationToken.None)
        do! runtime.RunToIdle()
        do! import

        let graduate = cart.GraduateCart(CancellationToken.None)
        do! runtime.RunToIdle()
        do! graduate
        Assert.Equal(2, runtime.StateOf(cart).Get<GraduatedCartState>("graduated").TotalQuantity)

        let reimport = cart.GetReimportedGraduatedState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! current = reimport

        Assert.Equal(2, current.TotalQuantity)
        Assert.Equal("USD", current.Currency)
        Assert.Equal("USD", runtime.StateOf(cart).Get<GraduatedCartStateV2>("graduated").Currency)
    }

    [<Fact>]
    member this.Fault_during_migration_leaves_seeded_node_and_next_turn_recovers() = task {
        use runtime = MigrationTests.CreateRuntime()
        let id = ActorId.Create("fault")
        let v1 = CartStateV1()
        v1.Skus <- ResizeArray(["sku-1"; "sku-2"])
        do! runtime.SeedStateAsync("MigratingCart", id, "cart", v1)
        runtime.Faults.FailNextUpcastHop<CartStateV1, CartStateV2>(stateName = "cart")
        let cart = MigrationTests.CreateCart(runtime, id)

        let failed = cart.GetState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! _ = Assert.ThrowsAsync<ActorInjectedTransientException>(Func<Task>(fun () -> failed :> Task))
        Assert.Equal(2, runtime.StateOf(cart).Get<CartStateV1>("cart").Skus.Count)

        let recovered = cart.GetState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! recoveredResult = recovered
        Assert.Equal(2, recoveredResult.TotalQuantity)
    }

    [<Fact>]
    member this.Full_disable_opt_out_stores_plain_legacy_type() = task {
        use runtime = MigrationTests.CreateRuntime(fun options -> options.DisableStateMigration <- true)
        let id = ActorId.Create("disabled")
        let cart = MigrationTests.CreateCart(runtime, id)

        let v1 = CartStateV1()
        v1.Skus <- ResizeArray(["sku-1"])
        let import = cart.ImportLegacyV1(v1, CancellationToken.None)
        do! runtime.RunToIdle()
        do! import

        let sku = runtime.StateOf(cart).Get<CartStateV1>("cart").Skus |> Seq.exactlyOne
        Assert.Equal("sku-1", sku)
        let! stored = MigrationTests.RawReadState<ActorStatePlainEnvelope<CartStateV1>>(runtime, "MigratingCart", id, "cart")
        Assert.Equal(ActorStateFormKind.Plain, stored.Header.FormKind)
    }

    [<Fact>]
    member this.Full_disable_still_reads_existing_enveloped_state_and_repersists_plain() = task {
        use runtime = MigrationTests.CreateRuntime(fun options -> options.DisableStateMigration <- true)
        let id = ActorId.Create("disabled-existing")
        let v1 = CartStateV1()
        v1.Skus <- ResizeArray(["sku-1"; "sku-1"])
        do! runtime.SeedStateAsync("MigratingCart", id, "cart", v1)
        let cart = MigrationTests.CreateCart(runtime, id)

        let read = cart.GetState(CancellationToken.None)
        do! runtime.RunToIdle()
        let! current = read

        Assert.Equal(2, current.TotalQuantity)
        let! stored = MigrationTests.RawReadState<ActorStatePlainEnvelope<CartStateV3>>(runtime, "MigratingCart", id, "cart")
        Assert.Equal(ActorStateFormKind.Plain, stored.Header.FormKind)
        Assert.Equal(2, stored.Value.TotalQuantity)
    }

    [<Fact>]
    member this.Full_disable_graduates_existing_enveloped_state_to_plain() = task {
        use runtime = MigrationTests.CreateRuntime(fun options -> options.DisableStateMigration <- true)
        let id = ActorId.Create("disabled-graduate")
        let g = GraduatedCartState()
        g.Lines <- ResizeArray([{ Sku = "sku-1"; Quantity = 2 }])
        g.TotalQuantity <- 2
        do! runtime.SeedStateAsync("MigratingCart", id, "graduated", g)
        let cart = MigrationTests.CreateCart(runtime, id)

        let graduate = cart.GraduateCart(CancellationToken.None)
        do! runtime.RunToIdle()
        do! graduate

        let! stored = MigrationTests.RawReadState<ActorStatePlainEnvelope<GraduatedCartState>>(runtime, "MigratingCart", id, "graduated")
        Assert.Equal(ActorStateFormKind.Plain, stored.Header.FormKind)
        Assert.Equal(2, stored.Value.TotalQuantity)
    }

    [<Fact>]
    member this.Interpreted_actor_state_round_trips_outside_typed_migration_path() = task {
        let store = InMemoryInterpretedMachineStore()
        let registry = CountingCapabilityRegistry()
        let id = ActorId.Create("machine")
        do! (InterpretedMachineDeployer(InterpretedMachineVerifier(registry), store)
                .DeployAsync("Machine", id, MigrationTests.InterpretedDefinition()))

        use runtime =
            new ActorTestRuntime(fun services ->
                services.AddDaprActorStateMigration(MigrationTests.InterpretedMigrationFamily()) |> ignore
                services.AddSingleton<IInterpretedMachineStore>(store) |> ignore
                services.AddSingleton<ICapabilityRegistry>(registry) |> ignore
                services.AddDaprInterpretedActors("Machine") |> ignore)

        do! MigrationTests.Raise(runtime, id, 3)
        let deactivate = runtime.InvokeAsync("Machine", id, "deactivate", "", ActorTurnKind.Deactivate)
        do! runtime.RunToIdle()
        let! _ = deactivate
        do! MigrationTests.Raise(runtime, id, 4)

        let interpreted = runtime.StateOf("Machine", id).Get<InterpretedActorState>("__interpreted")
        Assert.NotNull(interpreted)
        Assert.Equal(1, interpreted.DocumentVersion)
        Assert.Equal(2, interpreted.Data.["count"].GetInt32())
        let! stored = MigrationTests.RawReadState<ActorStatePlainEnvelope<InterpretedActorState>>(runtime, "Machine", id, "__interpreted")
        Assert.Equal(ActorStateFormKind.Plain, stored.Header.FormKind)
    }

    // ------------------------------------------------------------------
    // Reflection helpers — access internal ActorTestRuntime state.
    // ------------------------------------------------------------------

    static member private GetSerializerId(runtime: ActorTestRuntime) =
        let serializer = MigrationTests.GetPrivateProperty(runtime, "Serializer")
        (serializer.GetType().GetProperty("SerializerId").GetValue(serializer)) :?> string

    static member private GetSerializerVersion(runtime: ActorTestRuntime) =
        let serializer = MigrationTests.GetPrivateProperty(runtime, "Serializer")
        (serializer.GetType().GetProperty("SerializerVersion").GetValue(serializer)) :?> int

    static member private GetPrivateProperty(target: obj, name: string) =
        target.GetType().GetProperty(name, BindingFlags.Instance ||| BindingFlags.NonPublic).GetValue(target)

    static member private RawWriteState<'T>(runtime: ActorTestRuntime, actorType: string, actorId: ActorId, name: string, value: 'T) : ValueTask =
        let store = MigrationTests.GetPrivateProperty(runtime, "StateStore")
        let serializer = MigrationTests.GetPrivateProperty(runtime, "Serializer")
        let bytes = serializer.GetType().GetMethod("SerializeToBytes").MakeGenericMethod(typeof<'T>).Invoke(serializer, [| box value |]) :?> byte[]
        let write = store.GetType().GetMethod("WriteAsync")
        write.Invoke(store, [| box actorType; box actorId.Value; box name; box (ReadOnlyMemory<byte>(bytes)); box CancellationToken.None |]) :?> ValueTask

    static member private RawReadState<'T>(runtime: ActorTestRuntime, actorType: string, actorId: ActorId, name: string) : Task<'T> =
        task {
            let store = MigrationTests.GetPrivateProperty(runtime, "StateStore")
            let serializer = MigrationTests.GetPrivateProperty(runtime, "Serializer")
            let read = store.GetType().GetMethod("ReadAsync")
            let vt = read.Invoke(store, [| box actorType; box actorId.Value; box name; box CancellationToken.None |])
            let typedVt = vt :?> ValueTask<Nullable<ReadOnlyMemory<byte>>>
            let! bytes = typedVt
            if not bytes.HasValue then
                return Unchecked.defaultof<'T>
            else
                let bytesValue = bytes.Value
                return (serializer.GetType().GetMethod("DeserializeFromBytes").MakeGenericMethod(typeof<'T>).Invoke(serializer, [| box bytesValue |])) :?> 'T
        }

    static member private Raise(runtime: ActorTestRuntime, id: ActorId, value: int) : Task =
        task {
            let payload = JsonSerializer.Serialize(InterpretedEvent("add", MigrationTests.JsonValue(value)))
            let pending = runtime.InvokeAsync("Machine", id, "Raise", payload)
            do! runtime.RunToIdle()
            let! result = pending
            JsonDocument.Parse(Encoding.UTF8.GetString(result)) |> ignore
        }

    static member private JsonValue<'T>(value: 'T) : JsonElement =
        use document = JsonDocument.Parse(JsonSerializer.Serialize(value))
        document.RootElement.Clone()

    static member private InterpretedDefinition() : InterpretedMachineDefinition =
        InterpretedMachineDefinition(
            DocumentVersion = 1,
            InitialState = "Open",
            States = (ResizeArray([ InterpretedStateDefinition(Name = "Open") ]) :> IReadOnlyList<InterpretedStateDefinition>),
            Transitions = (ResizeArray([
                InterpretedTransitionDefinition(
                    Source = "Open",
                    Event = "add",
                    Branches = (ResizeArray([
                        InterpretedBranchDefinition(
                            Otherwise = true,
                            Effects = (ResizeArray([ "increment" ]) :> IReadOnlyList<string>)
                        )
                    ]) :> IReadOnlyList<InterpretedBranchDefinition>)
                )
            ]) :> IReadOnlyList<InterpretedTransitionDefinition>)
        )

    static member private InterpretedMigrationFamily() : IReadOnlyList<ActorStateMigrationFamilyRegistration> =
        let node = ActorStateMigrationNode(0, typeof<InterpretedActorState>, InterpretedActorStateShapeHash)
        ResizeArray([
            ActorStateMigrationFamilyRegistration(
                ActorStateMigrationFamily("InterpretedActorState", (ResizeArray([node]) :> IReadOnlyList<ActorStateMigrationNode>), (ResizeArray([]) :> IReadOnlyList<ActorStateMigrationEdge>)),
                (ResizeArray([
                    ActorStateNodeDeserializer(0, fun payload serializer ->
                        let envelope = serializer.DeserializeFromBytes<ActorStateEnvelope<InterpretedActorState>>(payload)
                        if isNull (box envelope) then null else box envelope.Value)
                ]) :> IReadOnlyList<ActorStateNodeDeserializer>),
                (ResizeArray([]) :> IReadOnlyList<ActorStateHopRegistration>)
            )
        ]) :> IReadOnlyList<ActorStateMigrationFamilyRegistration>

and CountingCapabilityRegistry() =
    let incrementEffect = IncrementEffect()

    interface ICapabilityRegistry with
        member this.TryGetEffect(name: string, effect: byref<IActorEffect>) : bool =
            effect <- incrementEffect
            String.Equals(name, "increment", StringComparison.Ordinal)

        member this.TryGetGuard(name: string, guard: byref<IActorGuard>) : bool =
            guard <- Unchecked.defaultof<_>
            false

and IncrementEffect() =
    interface IActorEffect with
        member _.ExecuteAsync(context: ActorCapabilityContext, _: CancellationToken) : ValueTask =
            let state = context.Arguments.["state"] :?> DynamicStateBag
            state.Set("count", state.Get<int>("count") + 1)
            ValueTask.CompletedTask
