open System
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Options
open Dapr.Actors.Next.Core.Client
open Cart.Next.FSharp.Example02

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()

builder.Services.AddDaprActors() |> ignore

let app: WebApplication = builder.Build()

let createCart (cartId: string) (proxies: IActorProxyFactory) =
    proxies.Create<IMigratingCartActor>(ActorId.Create(cartId), "MigratingCart")

app.MapGet("/", Func<string>(fun () -> "Migration actor sample. POST a legacy cart shape, then GET /carts/{cartId} to read the current shape.")) |> ignore

app.MapGet("/carts/{cartId}", Func<string, IActorProxyFactory, CancellationToken, Task<CartStateV3>>(fun cartId proxies ct ->
    (createCart cartId proxies).GetState(ct))) |> ignore

app.MapPost("/carts/{cartId}/legacy/v1", Func<string, CartStateV1, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId state proxies ct ->
    task {
        do! (createCart cartId proxies).ImportLegacyV1(state, ct)
        return Results.Accepted()
    })) |> ignore

app.MapPost("/carts/{cartId}/legacy/v2", Func<string, CartStateV2, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId state proxies ct ->
    task {
        do! (createCart cartId proxies).ImportLegacyV2(state, ct)
        return Results.Accepted()
    })) |> ignore

app.MapGet("/carts/{cartId}/autonomous", Func<string, IActorProxyFactory, CancellationToken, Task<MyStateV3>>(fun cartId proxies ct ->
    (createCart cartId proxies).GetAutonomousState(ct))) |> ignore

app.MapPost("/carts/{cartId}/autonomous/legacy", Func<string, MyState, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId state proxies ct ->
    task {
        do! (createCart cartId proxies).ImportAutonomousV1(state, ct)
        return Results.Accepted()
    })) |> ignore

app.MapGet("/carts/{cartId}/renamed", Func<string, IActorProxyFactory, CancellationToken, Task<RenamedStateV2>>(fun cartId proxies ct ->
    (createCart cartId proxies).GetRenamedState(ct))) |> ignore

app.MapPost("/carts/{cartId}/renamed/legacy", Func<string, RenamedState, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId state proxies ct ->
    task {
        do! (createCart cartId proxies).ImportRenamedV1(state, ct)
        return Results.Accepted()
    })) |> ignore

app.MapGet("/carts/{cartId}/graduated", Func<string, IActorProxyFactory, CancellationToken, Task<GraduatedCartState>>(fun cartId proxies ct ->
    (createCart cartId proxies).GetGraduatedState(ct))) |> ignore

app.MapPost("/carts/{cartId}/graduated", Func<string, GraduatedCartState, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId state proxies ct ->
    task {
        do! (createCart cartId proxies).ImportGraduated(state, ct)
        return Results.Accepted()
    })) |> ignore

app.MapPost("/carts/{cartId}/graduated/offramp", Func<string, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId proxies ct ->
    task {
        do! (createCart cartId proxies).GraduateCart(ct)
        return Results.Accepted()
    })) |> ignore

app.MapGet("/carts/{cartId}/graduated/reimported", Func<string, IActorProxyFactory, CancellationToken, Task<GraduatedCartStateV2>>(fun cartId proxies ct ->
    (createCart cartId proxies).GetReimportedGraduatedState(ct))) |> ignore

app.MapDelete("/carts/{cartId}", Func<string, IActorProxyFactory, CancellationToken, Task<IResult>>(fun cartId proxies ct ->
    task {
        do! (createCart cartId proxies).Clear(ct)
        return Results.NoContent()
    })) |> ignore

app.Run()
