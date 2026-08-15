open System
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Options
open Dapr.Actors.Next.Core.Client
open Cart.Next.FSharp.Example01

type StaticPricingClient() =
    interface IPricingClient with
        member _.GetPriceAsync(sku: string, _: CancellationToken) : ValueTask<decimal> =
            ValueTask.FromResult(if sku = "sku-1" then 12.50m else 1.00m)

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()

builder.Services.AddSingleton<IPricingClient, StaticPricingClient>() |> ignore
builder.Services.AddDaprActors() |> ignore

let app: WebApplication = builder.Build()

let createCart (cartId: string) (proxies: IActorProxyFactory) =
    proxies.Create<ICartActor>(ActorId.Create(cartId), "Cart")

app.MapGet("/", Func<string>(fun () -> "Cart actor sample. POST /carts/{cartId}/items and GET /carts/{cartId}/summary.")) |> ignore

app.MapPost("/carts/{cartId}/items", Func<string, CartItem, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId item proxies ct ->
    task {
        do! (createCart cartId proxies).AddItem(item, ct)
        return Results.NoContent()
    })) |> ignore

app.MapGet("/carts/{cartId}/summary", Func<string, IActorProxyFactory, CancellationToken, Task<CartSummary>>(fun cartId proxies ct ->
    (createCart cartId proxies).GetSummary(ct))) |> ignore

app.MapPost("/carts/{cartId}/abandon", Func<string, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId proxies ct ->
    task {
        do! (createCart cartId proxies).AbandonCart(ct)
        return Results.NoContent()
    })) |> ignore

app.Run()
