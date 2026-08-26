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

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Options
open Dapr.Actors.Next.Core.Client
open Dapr.Actors.Next.Streams
open Dapr.Client
open Dapr.Messaging.PublishSubscribe.Extensions
open Cart.Next.FSharp.Example03

type SkuRequest = { Sku: string }
type AvailabilityResponse = { CartId: string; Sku: string; Available: bool }
type RestockPublishResult = { CartId: string; Sku: string; AvailableAfterPublish: bool }

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()

builder.Services.AddDaprActors() |> ignore
builder.Services.AddDaprActorStreams() |> ignore
builder.Services.AddDaprPubSubClient() |> ignore
builder.Services.AddSingleton<DaprClient>(fun _ -> DaprClientBuilder().Build()) |> ignore

let app: WebApplication = builder.Build()

app.Services.GetRequiredService<ActorStreamSubscriptionRegistry>().Add(
    ActorStreamSubscription(
        RestockingCartNames.PubsubName,
        RestockingCartNames.RestockTopic,
        RestockingCartNames.ActorType,
        "OnRestock",
        "CartId")) |> ignore

let createCart (cartId: string) (proxies: IActorProxyFactory) =
    proxies.Create<IRestockingCartActor>(ActorId.Create(cartId), RestockingCartNames.ActorType)

let waitForAvailability (cart: IRestockingCartActor) (sku: string) (cancellationToken: CancellationToken) = task {
    let deadline = DateTimeOffset.UtcNow.AddSeconds(5.0)
    let mutable found = false
    while (not found) && (DateTimeOffset.UtcNow < deadline) do
        let! available = cart.IsAvailable(sku, cancellationToken)
        if available then
            found <- true
        else
            do! Task.Delay(100, cancellationToken)
    return found
}

app.MapGet("/", Func<string>(fun () -> "Pub/sub actor sample. POST /carts/{cartId}/unavailable, POST /inventory/restocked, then GET /carts/{cartId}.")) |> ignore

app.MapPost("/carts/{cartId}/unavailable", Func<string, SkuRequest, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId request proxies ct ->
    task {
        do! (createCart cartId proxies).AddUnavailableSku(request.Sku, ct)
        return Results.NoContent()
    })) |> ignore

app.MapGet("/carts/{cartId}", Func<string, IActorProxyFactory, CancellationToken, Task<RestockingCartState>>(fun cartId proxies ct ->
    (createCart cartId proxies).GetState(ct))) |> ignore

app.MapGet("/carts/{cartId}/items/{sku}/availability", Func<string, string, IActorProxyFactory, CancellationToken, Task<AvailabilityResponse>>(fun cartId sku proxies ct ->
    task {
        let! available = (createCart cartId proxies).IsAvailable(sku, ct)
        return { CartId = cartId; Sku = sku; Available = available }
    })) |> ignore

app.MapPost("/inventory/restocked", Func<RestockEvent, DaprClient, IActorProxyFactory, CancellationToken, Task<RestockPublishResult>>(fun evt dapr proxies ct ->
    task {
        do! dapr.PublishEventAsync(RestockingCartNames.PubsubName, RestockingCartNames.RestockTopic, evt, ct)
        let! available = waitForAvailability (createCart evt.CartId proxies) evt.Sku ct
        return { CartId = evt.CartId; Sku = evt.Sku; AvailableAfterPublish = available }
    })) |> ignore

app.MapDelete("/carts/{cartId}", Func<string, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId proxies ct ->
    task {
        do! (createCart cartId proxies).Clear(ct)
        return Results.NoContent()
    })) |> ignore

app.Run()
