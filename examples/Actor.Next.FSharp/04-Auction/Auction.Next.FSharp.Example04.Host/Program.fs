open System
open System.Text.Json.Serialization
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Json
open Microsoft.Extensions.DependencyInjection
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Options
open Dapr.Actors.Next.Core.Client
open Auction.Next.FSharp.Example04

type BidResponse = { AuctionId: string; Amount: decimal; Bidder: string; Result: BidResult }

type AuctionSnapshot = {
    AuctionId: string
    State: AuctionState
    HighBid: decimal
    HighBidder: string
    FulfillmentStarted: bool
}

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()

builder.Services.AddDaprActors() |> ignore
builder.Services.ConfigureHttpJsonOptions(fun options ->
    options.SerializerOptions.Converters.Add(JsonStringEnumConverter()) |> ignore) |> ignore

let app: WebApplication = builder.Build()

let createAuction (auctionId: string) (proxies: IActorProxyFactory) =
    proxies.Create<IAuctionActor>(ActorId.Create(auctionId), "Auction")

let readSnapshot (proxies: IActorProxyFactory) (auctionId: string) (cancellationToken: CancellationToken) = task {
    let auction = createAuction auctionId proxies
    let! state = auction.GetState(cancellationToken)
    let! data = auction.GetData(cancellationToken)
    return { AuctionId = auctionId; State = state; HighBid = data.HighBid; HighBidder = data.HighBidder; FulfillmentStarted = data.FulfillmentStarted }
}

app.MapGet("/", Func<string>(fun () -> "Auction actor sample. POST /auctions/{auctionId}/bids, then GET /auctions/{auctionId}, close, or expire.")) |> ignore

app.MapPost("/auctions/{auctionId}/bids", Func<string, Bid, IActorProxyFactory, CancellationToken, Task<IResult>>(fun auctionId bid proxies ct ->
    task {
        let! result = (createAuction auctionId proxies).PlaceBid(bid, ct)
        return Results.Ok({ AuctionId = auctionId; Amount = bid.Amount; Bidder = bid.Bidder; Result = result })
    })) |> ignore

app.MapGet("/auctions/{auctionId}", Func<string, IActorProxyFactory, CancellationToken, Task<AuctionSnapshot>>(fun auctionId proxies ct ->
    task {
        let auction = createAuction auctionId proxies
        let! state = auction.GetState(ct)
        let! data = auction.GetData(ct)
        return { AuctionId = auctionId; State = state; HighBid = data.HighBid; HighBidder = data.HighBidder; FulfillmentStarted = data.FulfillmentStarted }
    })) |> ignore

app.MapPost("/auctions/{auctionId}/close", Func<string, IActorProxyFactory, CancellationToken, Task<IResult>>(fun auctionId proxies ct ->
    task {
        do! (createAuction auctionId proxies).Close(ct)
        let! snapshot = readSnapshot proxies auctionId ct
        return Results.Ok(snapshot)
    })) |> ignore

app.MapPost("/auctions/{auctionId}/expire", Func<string, IActorProxyFactory, CancellationToken, Task<IResult>>(fun auctionId proxies ct ->
    task {
        do! (createAuction auctionId proxies).Expire(ct)
        let! snapshot = readSnapshot proxies auctionId ct
        return Results.Ok(snapshot)
    })) |> ignore

app.Run()