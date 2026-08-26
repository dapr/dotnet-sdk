namespace Cart.Next.FSharp.Example01

open System
open System.Collections.Generic
open System.Linq
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Attributes
open Dapr.Actors.Next.Abstractions.State
open Dapr.Actors.Next.Core.Activation
open Dapr.Actors.Next.Core.Timers

// Payload types must be defined before the interface that references them (F# has no forward references)
type CartItem = { Sku: string; Quantity: int }
type CartSummary = { ItemCount: int; Total: decimal; Abandoned: bool }

type IPricingClient =
    abstract member GetPriceAsync: string * CancellationToken -> ValueTask<decimal>

type CartState() =
    member val Items = Dictionary<string, int>() with get, set
    member val Prices = Dictionary<string, decimal>() with get, set
    member val Abandoned = false with get, set

[<GenerateActorClient>]
type ICartActor =
    inherit IActor
    abstract member AddItem: item: CartItem * cancellationToken: CancellationToken -> Task
    abstract member GetSummary: cancellationToken: CancellationToken -> Task<CartSummary>
    abstract member AbandonCart: cancellationToken: CancellationToken -> Task

[<DaprActor("Cart")>]
type CartActor(context: ActorActivationContext, pricing: IPricingClient, timers: IActorTimerScheduler) =
    inherit Actor()

    static member private AbandonAfter = TimeSpan.FromMinutes(20.0)

    override this.Id = context.ActorId
    override this.State = context.State

    interface ICartActor with
        member this.AddItem(item, cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync("cart", (fun () -> CartState()), cancellationToken)
            let prevQty = 
                match state.Value.Items.TryGetValue(item.Sku) with
                | true, v -> v
                | false, _ -> 0
            state.Value.Items.[item.Sku] <- prevQty + item.Quantity
            let! price = pricing.GetPriceAsync(item.Sku, cancellationToken)
            state.Value.Prices.[item.Sku] <- price
            state.Value.Abandoned <- false

            do! timers.RescheduleAsync(
                "Cart", this.Id, "abandon-cart",
                CartActor.AbandonAfter, "AbandonCart", "",
                cancellationToken = cancellationToken)
        }

        member this.GetSummary(cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync("cart", (fun () -> CartState()), cancellationToken)
            let itemCount = state.Value.Items.Values.Sum()
            let total =
                state.Value.Items.Sum(fun kv ->
                    let price =
                        match state.Value.Prices.TryGetValue(kv.Key) with
                        | true, p -> p
                        | false, _ -> 0m
                    decimal kv.Value * price)
            return { ItemCount = itemCount; Total = total; Abandoned = state.Value.Abandoned }
        }

        member this.AbandonCart(cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync("cart", (fun () -> CartState()), cancellationToken)
            state.Value.Abandoned <- true
        }
