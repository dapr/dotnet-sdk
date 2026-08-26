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

namespace Cart.Next.FSharp.Example03.Tests

open System
open System.Collections.Generic
open System.Reflection
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Options
open Dapr.Actors.Next.Core.Client
open Dapr.Actors.Next.Streams
open Dapr.Actors.Next.Testing
open Microsoft.Extensions.DependencyInjection
open Cart.Next.FSharp.Example03
open Xunit

type PubSubTests() =

    static member private Subscription =
        ActorStreamSubscription(
            RestockingCartNames.PubsubName,
            RestockingCartNames.RestockTopic,
            RestockingCartNames.ActorType,
            "OnRestock",
            "CartId")

    static member private WebJsonOptions = JsonSerializerOptions(JsonSerializerDefaults.Web)

    static member private CreateRuntime() =
        let _ = typeof<RestockingCartActor>
        let glueAssembly = Assembly.Load("Cart.Next.FSharp.Example03.Glue")
        let moduleType = glueAssembly.GetType("Dapr.Actors.Next.Generated.GeneratedActorRegistrationModule")
        match moduleType with
        | null -> failwith "GeneratedActorRegistrationModule type not found in Glue assembly"
        | t ->
            let registerMethod = t.GetMethod("Register", BindingFlags.Static ||| BindingFlags.NonPublic)
            match registerMethod with
            | null -> failwith "Register method not found on GeneratedActorRegistrationModule"
            | m -> m.Invoke(null, null) |> ignore
        ActorTestRuntime(fun services ->
            services.AddDaprActors(Action<DaprActorsOptions>(fun _ -> ())) |> ignore)

    static member private Runner(runtime: ActorTestRuntime) =
        let prop = typeof<ActorTestRuntime>.GetProperty("Runtime", BindingFlags.Instance ||| BindingFlags.NonPublic)
        match prop with
        | null -> failwith "Runtime property not found on ActorTestRuntime"
        | p ->
            let value = p.GetValue(runtime)
            match value with
            | null -> failwith "Runtime property value is null"
            | :? IActorInvocationClient as client ->
                ActorStreamSubscriptionRunner(
                    ActorStreamForwarder(client, ActorStreamRoutingKeyExtractor()),
                    DefaultActorStreamFailureClassifier())
            | _ -> failwith "Runtime is not an IActorInvocationClient"

    static member private Event(evt: RestockEvent) =
        ActorStreamEvent(
            "event-1",
            RestockingCartNames.PubsubName,
            RestockingCartNames.RestockTopic,
            ReadOnlyMemory(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt, PubSubTests.WebJsonOptions))),
            Dictionary<string, string>() :> IReadOnlyDictionary<string, string>)

    static member private AddSku(runtime: ActorTestRuntime, cart: IRestockingCartActor, sku: string) = task {
        let add = cart.AddUnavailableSku(sku, CancellationToken.None)
        do! runtime.RunToIdle()
        do! add
    }

    static member private IsAvailable(runtime: ActorTestRuntime, cart: IRestockingCartActor, sku: string) = task {
        let read = cart.IsAvailable(sku, CancellationToken.None)
        do! runtime.RunToIdle()
        let! result = read
        return result
    }

    [<Fact>]
    member this.Publishing_one_event_wakes_only_the_named_cart() = task {
        use runtime = PubSubTests.CreateRuntime()
        let named = runtime.CreateActor<IRestockingCartActor>(ActorId.Create("cart-1"), RestockingCartNames.ActorType)
        let other = runtime.CreateActor<IRestockingCartActor>(ActorId.Create("cart-2"), RestockingCartNames.ActorType)
        do! PubSubTests.AddSku(runtime, named, "sku-1")
        do! PubSubTests.AddSku(runtime, other, "sku-1")

        let delivery = (PubSubTests.Runner(runtime)).ProcessEventAsync(PubSubTests.Subscription, PubSubTests.Event({ CartId = "cart-1"; Sku = "sku-1" }))
        do! runtime.RunToIdle()
        let! action = delivery

        Assert.Equal(ActorStreamDeliveryAction.Ack, action)
        let! namedAvailable = PubSubTests.IsAvailable(runtime, named, "sku-1")
        Assert.True(namedAvailable)
        let! otherAvailable = PubSubTests.IsAvailable(runtime, other, "sku-1")
        Assert.False(otherAvailable)
    }

    [<Fact>]
    member this.Transient_state_write_fault_retries_delivery_instead_of_acknowledging() = task {
        use runtime = PubSubTests.CreateRuntime()
        let cart = runtime.CreateActor<IRestockingCartActor>(ActorId.Create("cart-retry"), RestockingCartNames.ActorType)
        do! PubSubTests.AddSku(runtime, cart, "sku-1")

        runtime.Faults.FailNextStateWrite<RestockingCartState>()
        let firstDelivery = (PubSubTests.Runner(runtime)).ProcessEventAsync(PubSubTests.Subscription, PubSubTests.Event({ CartId = "cart-retry"; Sku = "sku-1" }))
        do! runtime.RunToIdle()
        let! first = firstDelivery
        let secondDelivery = (PubSubTests.Runner(runtime)).ProcessEventAsync(PubSubTests.Subscription, PubSubTests.Event({ CartId = "cart-retry"; Sku = "sku-1" }))
        do! runtime.RunToIdle()
        let! second = secondDelivery

        Assert.Equal(ActorStreamDeliveryAction.Retry, first)
        Assert.Equal(ActorStreamDeliveryAction.Ack, second)
        let! available = PubSubTests.IsAvailable(runtime, cart, "sku-1")
        Assert.True(available)
    }
