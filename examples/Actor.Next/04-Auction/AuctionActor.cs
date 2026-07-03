using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.StateMachine;

namespace Dapr.Actors.Next.Examples.Auction;

[GenerateActorClient]
public interface IAuctionActor : IActor
{
    Task<BidResult> PlaceBid(Bid bid, CancellationToken cancellationToken = default);

    Task Close(CancellationToken cancellationToken = default);

    Task Expire(CancellationToken cancellationToken = default);

    Task<AuctionState> GetState(CancellationToken cancellationToken = default);

    Task<AuctionData> GetData(CancellationToken cancellationToken = default);
}

public enum AuctionState
{
    Open,
    Sold,
    Expired,
}

public sealed record AuctionData(decimal HighBid, string? HighBidder, bool FulfillmentStarted)
{
    public static AuctionData Empty { get; } = new(0, null, false);
}

public sealed record Bid(decimal Amount, string Bidder);

public sealed record CloseAuction;

public sealed record ExpireAuction;

public enum BidResult
{
    Accepted,
    TooLow,
    Closed,
}

[DaprActor("Auction")]
public sealed class AuctionActor : StateMachineActor<AuctionState, AuctionData>, IAuctionActor
{
    private readonly IActorTimerScheduler timers;
    private static readonly TimeSpan SoftClose = TimeSpan.FromSeconds(30);

    public AuctionActor(ActorActivationContext context, IActorTimerScheduler timers)
        : base(context, timers, "Auction", AuctionData.Empty)
    {
        this.timers = timers;
    }

    protected override void Configure(IStateMachine<AuctionState, AuctionData> sm)
    {
        sm.InitialState(AuctionState.Open);

        sm.In(AuctionState.Open)
            .On<Bid>()
                .When((data, bid) => bid.Amount > data.HighBid)
                    .Do(async ctx =>
                    {
                        ctx.Update(data => data with { HighBid = ctx.Event.Amount, HighBidder = ctx.Event.Bidder });
                        await timers.RescheduleAsync("Auction", Id, "soft-close", SoftClose, nameof(Close), string.Empty);
                        ctx.Reply(BidResult.Accepted);
                    })
                .Otherwise()
                    .Reply(BidResult.TooLow);

        sm.In(AuctionState.Open)
            .On<CloseAuction>().GoTo(AuctionState.Sold);

        sm.In(AuctionState.Open)
            .On<ExpireAuction>().GoTo(AuctionState.Expired);

        sm.In(AuctionState.Sold)
            .OnEntry(ctx => ctx.Update(data => data with { FulfillmentStarted = true }))
            .Ignore<Bid>()
            .Ignore<CloseAuction>()
            .Ignore<ExpireAuction>();

        sm.In(AuctionState.Expired)
            .Ignore<Bid>()
            .Ignore<CloseAuction>()
            .Ignore<ExpireAuction>();
    }

    public Task<BidResult> PlaceBid(Bid bid, CancellationToken cancellationToken = default)
    {
        if (CurrentState != AuctionState.Open)
        {
            return Task.FromResult(BidResult.Closed);
        }

        return Raise<BidResult>(bid, cancellationToken);
    }

    public Task Close(CancellationToken cancellationToken = default) =>
        Raise<object?>(new CloseAuction(), cancellationToken);

    public Task Expire(CancellationToken cancellationToken = default) =>
        Raise<object?>(new ExpireAuction(), cancellationToken);

    public Task<AuctionState> GetState(CancellationToken cancellationToken = default) =>
        Task.FromResult(CurrentState);

    public Task<AuctionData> GetData(CancellationToken cancellationToken = default) =>
        Task.FromResult(Data);
}

public sealed class BadAuctionActor(ActorActivationContext context, IActorTimerScheduler timers) :
    StateMachineActor<AuctionState, AuctionData>(context, timers, "BadAuction", AuctionData.Empty),
    IAuctionActor
{
    protected override void Configure(IStateMachine<AuctionState, AuctionData> sm)
    {
        sm.InitialState(AuctionState.Open);
        sm.In(AuctionState.Open).On<Bid>().When((_, bid) => bid.Amount > 0).GoTo(AuctionState.Sold);
        sm.In(AuctionState.Sold);
        sm.In(AuctionState.Expired);
    }

    public Task<BidResult> PlaceBid(Bid bid, CancellationToken cancellationToken = default) => Raise<BidResult>(bid, cancellationToken);

    public Task Close(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task Expire(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<AuctionState> GetState(CancellationToken cancellationToken = default) => Task.FromResult(CurrentState);

    public Task<AuctionData> GetData(CancellationToken cancellationToken = default) => Task.FromResult(Data);
}
