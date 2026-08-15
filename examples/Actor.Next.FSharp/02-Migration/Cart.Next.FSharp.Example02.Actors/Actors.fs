namespace Cart.Next.FSharp.Example02

open System
open System.Linq
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Attributes
open Dapr.Actors.Next.Abstractions.State
open Dapr.Actors.Next.Core.Activation

// --------------------------------------------------------------------------
// State model types. F# classes with member val are used (rather than records)
// to provide non-null default values that match the C# init defaults, which
// System.Text.Json preserves when a JSON field is absent during deserialization.
// The V-suffixed names are required by the source generator's migration-family
// discovery (it parses the trailing V<n> as the chain version).
// --------------------------------------------------------------------------

type CartLine = { Sku: string; Quantity: int }

type CartStateV1() =
    member val Skus = ResizeArray<string>() with get, set

type CartStateV2() =
    member val Lines = ResizeArray<CartLine>() with get, set

type CartStateV3() =
    member val Lines = ResizeArray<CartLine>() with get, set
    member val TotalQuantity = 0 with get, set

type MyState() =
    member val Name = "" with get, set

type MyStateV2() =
    member val Name = "" with get, set
    member val Age = 0 with get, set

type MyStateV3() =
    member val Name = "" with get, set
    member val Age = 0 with get, set
    member val Active = false with get, set

type RenamedState() =
    member val FirstName = "" with get, set
    member val LastName = "" with get, set

type RenamedStateV2() =
    member val DisplayName = "" with get, set

type GraduatedCartState() =
    member val Lines = ResizeArray<CartLine>() with get, set
    member val TotalQuantity = 0 with get, set

type GraduatedCartStateV2() =
    member val Lines = ResizeArray<CartLine>() with get, set
    member val TotalQuantity = 0 with get, set
    member val Currency = "USD" with get, set

// --------------------------------------------------------------------------
// Upcasters. The F# Glue-project pattern relies on the source generator's
// scan-references path, which discovers state types only through upcaster
// From/To types. Therefore every migration family needs at least one explicit
// upcaster edge so the generator builds the family and its nodes.
// --------------------------------------------------------------------------

type CartStateV1ToV2() =
    interface IActorStateUpcaster<CartStateV1, CartStateV2> with
        member _.UpcastAsync(state: CartStateV1, _: CancellationToken) : ValueTask<CartStateV2> =
            let lines =
                state.Skus
                |> Seq.countBy id
                |> Seq.map (fun (sku, count) -> { Sku = sku; Quantity = count })
                |> ResizeArray
            let v2 = CartStateV2()
            v2.Lines <- lines
            ValueTask.FromResult(v2)

type CartStateV2ToV3() =
    interface IActorStateUpcaster<CartStateV2, CartStateV3> with
        member _.UpcastAsync(state: CartStateV2, _: CancellationToken) : ValueTask<CartStateV3> =
            let v3 = CartStateV3()
            v3.Lines <- ResizeArray(state.Lines)
            v3.TotalQuantity <- state.Lines.Sum(fun line -> line.Quantity)
            ValueTask.FromResult(v3)

type MyStateToMyStateV2() =
    interface IActorStateUpcaster<MyState, MyStateV2> with
        member _.UpcastAsync(state: MyState, _: CancellationToken) : ValueTask<MyStateV2> =
            let v2 = MyStateV2()
            v2.Name <- state.Name
            ValueTask.FromResult(v2)

type MyStateV2ToMyStateV3() =
    interface IActorStateUpcaster<MyStateV2, MyStateV3> with
        member _.UpcastAsync(state: MyStateV2, _: CancellationToken) : ValueTask<MyStateV3> =
            let v3 = MyStateV3()
            v3.Name <- state.Name
            v3.Age <- state.Age
            ValueTask.FromResult(v3)

type RenamedStateToV2() =
    interface IActorStateUpcaster<RenamedState, RenamedStateV2> with
        member _.UpcastAsync(state: RenamedState, _: CancellationToken) : ValueTask<RenamedStateV2> =
            let parts =
                [state.FirstName; state.LastName]
                |> Seq.filter (fun p -> p.Length > 0)
            let v2 = RenamedStateV2()
            v2.DisplayName <- String.Join(" ", parts)
            ValueTask.FromResult(v2)

type GraduatedCartStateToV2() =
    interface IActorStateUpcaster<GraduatedCartState, GraduatedCartStateV2> with
        member _.UpcastAsync(state: GraduatedCartState, _: CancellationToken) : ValueTask<GraduatedCartStateV2> =
            let v2 = GraduatedCartStateV2()
            v2.Lines <- ResizeArray(state.Lines)
            v2.TotalQuantity <- state.TotalQuantity
            ValueTask.FromResult(v2)

// --------------------------------------------------------------------------
// Actor interface (generated proxy) and implementation.
// --------------------------------------------------------------------------

[<GenerateActorClient>]
type IMigratingCartActor =
    inherit IActor
    abstract member GetState: cancellationToken: CancellationToken -> Task<CartStateV3>
    abstract member TryGetState: cancellationToken: CancellationToken -> Task<CartStateV3>
    abstract member ImportLegacyV1: state: CartStateV1 * cancellationToken: CancellationToken -> Task
    abstract member ImportLegacyV2: state: CartStateV2 * cancellationToken: CancellationToken -> Task
    abstract member AddSku: sku: string * cancellationToken: CancellationToken -> Task
    abstract member GetAutonomousState: cancellationToken: CancellationToken -> Task<MyStateV3>
    abstract member ImportAutonomousV1: state: MyState * cancellationToken: CancellationToken -> Task
    abstract member GetRenamedState: cancellationToken: CancellationToken -> Task<RenamedStateV2>
    abstract member ImportRenamedV1: state: RenamedState * cancellationToken: CancellationToken -> Task
    abstract member GetGraduatedState: cancellationToken: CancellationToken -> Task<GraduatedCartState>
    abstract member GetReimportedGraduatedState: cancellationToken: CancellationToken -> Task<GraduatedCartStateV2>
    abstract member GraduateCart: cancellationToken: CancellationToken -> Task
    abstract member ImportGraduated: state: GraduatedCartState * cancellationToken: CancellationToken -> Task
    abstract member Clear: cancellationToken: CancellationToken -> Task

[<DaprActor("MigratingCart")>]
type MigratingCartActor(context: ActorActivationContext) =
    inherit Actor()

    static member private CartStateName = "cart"
    static member private AutonomousStateName = "autonomous"
    static member private RenamedStateName = "renamed"
    static member private GraduatedStateName = "graduated"

    override this.Id = context.ActorId
    override this.State = context.State

    interface IMigratingCartActor with
        member this.GetState(cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync(MigratingCartActor.CartStateName, (fun () -> CartStateV3()), cancellationToken)
            return state.Value
        }

        member this.TryGetState(cancellationToken) = task {
            let! existing = this.State.TryGetAsync<CartStateV3>(MigratingCartActor.CartStateName, cancellationToken)
            match existing with
            | null -> return CartStateV3()
            | state -> return state.Value
        }

        member this.ImportLegacyV1(state, cancellationToken) =
            this.State.SetAsync(MigratingCartActor.CartStateName, state, cancellationToken).AsTask()

        member this.ImportLegacyV2(state, cancellationToken) =
            this.State.SetAsync(MigratingCartActor.CartStateName, state, cancellationToken).AsTask()

        member this.AddSku(sku, cancellationToken) = task {
            let! cart = this.State.GetOrCreateAsync(MigratingCartActor.CartStateName, (fun () -> CartStateV3()), cancellationToken)
            let lines = ResizeArray(cart.Value.Lines)
            let existing = lines.FindIndex(fun line -> String.Equals(line.Sku, sku, StringComparison.Ordinal))
            if existing >= 0 then
                let line = lines.[existing]
                lines.[existing] <- { line with Quantity = line.Quantity + 1 }
            else
                lines.Add({ Sku = sku; Quantity = 1 })

            let newState = CartStateV3()
            newState.Lines <- lines
            newState.TotalQuantity <- lines.Sum(fun line -> line.Quantity)
            cart.Value <- newState
        }

        member this.GetAutonomousState(cancellationToken) = task {
            let! state =
                this.State.GetOrCreateAsync(
                    MigratingCartActor.AutonomousStateName,
                    (fun () ->
                        let v = MyStateV3()
                        v.Name <- "default"
                        v.Active <- true
                        v),
                    cancellationToken)
            return state.Value
        }

        member this.ImportAutonomousV1(state, cancellationToken) =
            this.State.SetAsync(MigratingCartActor.AutonomousStateName, state, cancellationToken).AsTask()

        member this.GetRenamedState(cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync(MigratingCartActor.RenamedStateName, (fun () -> RenamedStateV2()), cancellationToken)
            return state.Value
        }

        member this.ImportRenamedV1(state, cancellationToken) =
            this.State.SetAsync(MigratingCartActor.RenamedStateName, state, cancellationToken).AsTask()

        member this.GetGraduatedState(cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync(MigratingCartActor.GraduatedStateName, (fun () -> GraduatedCartState()), cancellationToken)
            return state.Value
        }

        member this.GetReimportedGraduatedState(cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync(MigratingCartActor.GraduatedStateName, (fun () -> GraduatedCartStateV2()), cancellationToken)
            return state.Value
        }

        member this.GraduateCart(cancellationToken) =
            this.State.GraduateAsync<GraduatedCartState>(MigratingCartActor.GraduatedStateName, cancellationToken).AsTask()

        member this.ImportGraduated(state, cancellationToken) =
            this.State.SetAsync(MigratingCartActor.GraduatedStateName, state, cancellationToken).AsTask()

        member this.Clear(cancellationToken) = task {
            do! this.State.RemoveAsync(MigratingCartActor.CartStateName, cancellationToken)
            do! this.State.RemoveAsync(MigratingCartActor.AutonomousStateName, cancellationToken)
            do! this.State.RemoveAsync(MigratingCartActor.RenamedStateName, cancellationToken)
            do! this.State.RemoveAsync(MigratingCartActor.GraduatedStateName, cancellationToken)
        }
