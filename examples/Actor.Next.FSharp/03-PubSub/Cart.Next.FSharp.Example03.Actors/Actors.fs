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

namespace Cart.Next.FSharp.Example03

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Attributes
open Dapr.Actors.Next.Abstractions.State
open Dapr.Actors.Next.Core.Activation

module RestockingCartNames =
    [<Literal>]
    let ActorType = "RestockingCart"

    [<Literal>]
    let PubsubName = "orders-pubsub"

    [<Literal>]
    let RestockTopic = "inventory-restocked"

type RestockEvent = { CartId: string; Sku: string }

type RestockingCartState() =
    member val WaitingForStock = HashSet<string>() with get, set
    member val Available = HashSet<string>() with get, set

[<GenerateActorClient>]
type IRestockingCartActor =
    inherit IActor
    abstract member AddUnavailableSku: sku: string * cancellationToken: CancellationToken -> Task
    abstract member OnRestock: evt: RestockEvent * cancellationToken: CancellationToken -> Task
    abstract member IsAvailable: sku: string * cancellationToken: CancellationToken -> Task<bool>
    abstract member GetState: cancellationToken: CancellationToken -> Task<RestockingCartState>
    abstract member Clear: cancellationToken: CancellationToken -> Task

[<DaprActor(RestockingCartNames.ActorType)>]
type RestockingCartActor(context: ActorActivationContext) =
    inherit Actor()

    override this.Id = context.ActorId
    override this.State = context.State

    interface IRestockingCartActor with
        member this.AddUnavailableSku(sku, cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync("cart", (fun () -> RestockingCartState()), cancellationToken)
            state.Value.WaitingForStock.Add(sku) |> ignore
            state.Value.Available.Remove(sku) |> ignore
        }

        [<Subscribe(RestockingCartNames.PubsubName, RestockingCartNames.RestockTopic, RouteBy = "CartId")>]
        member this.OnRestock(evt, cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync("cart", (fun () -> RestockingCartState()), cancellationToken)
            if state.Value.WaitingForStock.Remove(evt.Sku) then
                state.Value.Available.Add(evt.Sku) |> ignore
        }

        member this.IsAvailable(sku, cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync("cart", (fun () -> RestockingCartState()), cancellationToken)
            return state.Value.Available.Contains(sku)
        }

        member this.GetState(cancellationToken) = task {
            let! state = this.State.GetOrCreateAsync("cart", (fun () -> RestockingCartState()), cancellationToken)
            let copy = RestockingCartState()
            copy.WaitingForStock <- HashSet<string>(state.Value.WaitingForStock)
            copy.Available <- HashSet<string>(state.Value.Available)
            return copy
        }

        member this.Clear(cancellationToken) = task {
            do! this.State.RemoveAsync("cart", cancellationToken)
        }
