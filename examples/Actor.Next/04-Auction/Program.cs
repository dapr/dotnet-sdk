using System.Text.Json.Serialization;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Examples.Auction;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprActors();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.MapGet("/", () => "Auction actor sample. POST /auctions/{auctionId}/bids, then GET /auctions/{auctionId}, close, or expire.");

app.MapPost("/auctions/{auctionId}/bids", async (
    string auctionId,
    Bid bid,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    var result = await CreateAuction(proxies, auctionId).PlaceBid(bid, cancellationToken);
    return Results.Ok(new BidResponse(auctionId, bid.Amount, bid.Bidder, result));
});

app.MapGet("/auctions/{auctionId}", async (
    string auctionId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    var auction = CreateAuction(proxies, auctionId);
    var state = await auction.GetState(cancellationToken);
    var data = await auction.GetData(cancellationToken);
    return Results.Ok(new AuctionSnapshot(auctionId, state, data.HighBid, data.HighBidder, data.FulfillmentStarted));
});

app.MapPost("/auctions/{auctionId}/close", async (
    string auctionId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateAuction(proxies, auctionId).Close(cancellationToken);
    return Results.Ok(await ReadSnapshot(proxies, auctionId, cancellationToken));
});

app.MapPost("/auctions/{auctionId}/expire", async (
    string auctionId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateAuction(proxies, auctionId).Expire(cancellationToken);
    return Results.Ok(await ReadSnapshot(proxies, auctionId, cancellationToken));
});

await app.RunAsync();
return;

static IAuctionActor CreateAuction(IActorProxyFactory proxies, string auctionId) =>
    proxies.Create<IAuctionActor>(ActorId.Create(auctionId), "Auction");

static async Task<AuctionSnapshot> ReadSnapshot(
    IActorProxyFactory proxies,
    string auctionId,
    CancellationToken cancellationToken)
{
    var auction = CreateAuction(proxies, auctionId);
    var state = await auction.GetState(cancellationToken);
    var data = await auction.GetData(cancellationToken);
    return new AuctionSnapshot(auctionId, state, data.HighBid, data.HighBidder, data.FulfillmentStarted);
}

internal sealed record BidResponse(string AuctionId, decimal Amount, string Bidder, BidResult Result);

internal sealed record AuctionSnapshot(
    string AuctionId,
    AuctionState State,
    decimal HighBid,
    string? HighBidder,
    bool FulfillmentStarted);
