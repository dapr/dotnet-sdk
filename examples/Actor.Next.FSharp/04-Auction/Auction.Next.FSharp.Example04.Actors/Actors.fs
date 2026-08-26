namespace Auction.Next.FSharp.Example04

open System
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Attributes
open Dapr.Actors.Next.Core.Activation
open Dapr.Actors.Next.Core.Timers
open Dapr.Actors.Next.StateMachine

// Enums must be defined before records that may reference them
type AuctionState =
    | Open = 0
    | Sold = 1
    | Expired = 2

type BidResult =
    | Accepted = 0
    | TooLow = 1
    | Closed = 2

// Payload types must be defined before the interface that references them (F# has no forward references)
type Bid = { Amount: decimal; Bidder: string }

[<Sealed>]
type CloseAuction() = class end

[<Sealed>]
type ExpireAuction() = class end

type AuctionData = {
    HighBid: decimal
    HighBidder: string
    FulfillmentStarted: bool
} with
    static member Empty = { HighBid = 0m; HighBidder = Unchecked.defaultof<string>; FulfillmentStarted = false }

[<GenerateActorClient>]
type IAuctionActor =
    inherit IActor
    abstract member PlaceBid: bid: Bid * cancellationToken: CancellationToken -> Task<BidResult>
    abstract member Close: cancellationToken: CancellationToken -> Task
    abstract member Expire: cancellationToken: CancellationToken -> Task
    abstract member GetState: cancellationToken: CancellationToken -> Task<AuctionState>
    abstract member GetData: cancellationToken: CancellationToken -> Task<AuctionData>

[<DaprActor("Auction")>]
type AuctionActor(context: ActorActivationContext, timers: IActorTimerScheduler) =
    inherit StateMachineActor<AuctionState, AuctionData>(context, timers, "Auction", AuctionData.Empty)

    static member private SoftClose = TimeSpan.FromSeconds(30.0)

    // F# does not allow accessing protected members (Id) from inside lambdas, so expose it via a helper.
    member private this.GetActorId() = this.Id

    override this.Configure(sm: IStateMachine<AuctionState, AuctionData>) =
        sm.InitialState(AuctionState.Open) |> ignore

        sm.In(AuctionState.Open)
            .On<Bid>()
            .When(fun data bid -> bid.Amount > data.HighBid)
            .Do(fun ctx ->
                let work = task {
                    ctx.Update(fun data -> { data with HighBid = ctx.Event.Amount; HighBidder = ctx.Event.Bidder })
                    do! timers.RescheduleAsync("Auction", this.GetActorId(), "soft-close", AuctionActor.SoftClose, "Close", "")
                    ctx.Reply(BidResult.Accepted)
                }
                ValueTask(work)
            )
            .Otherwise()
            .Reply(BidResult.TooLow)
            |> ignore

        sm.In(AuctionState.Open)
            .On<CloseAuction>()
            .GoTo(AuctionState.Sold)
            |> ignore

        sm.In(AuctionState.Open)
            .On<ExpireAuction>()
            .GoTo(AuctionState.Expired)
            |> ignore

        sm.In(AuctionState.Sold)
            .OnEntry(fun ctx -> ctx.Update(fun data -> { data with FulfillmentStarted = true }))
            .Ignore<Bid>()
            .Ignore<CloseAuction>()
            .Ignore<ExpireAuction>()
            |> ignore

        sm.In(AuctionState.Expired)
            .Ignore<Bid>()
            .Ignore<CloseAuction>()
            .Ignore<ExpireAuction>()
            |> ignore

    interface IAuctionActor with
        member this.PlaceBid(bid: Bid, cancellationToken: CancellationToken) : Task<BidResult> =
            if this.CurrentState <> AuctionState.Open then
                Task.FromResult(BidResult.Closed)
            else
                this.Raise<BidResult>(bid, cancellationToken)

        member this.Close(cancellationToken: CancellationToken) : Task =
            this.Raise<obj>(CloseAuction(), cancellationToken) :> Task

        member this.Expire(cancellationToken: CancellationToken) : Task =
            this.Raise<obj>(ExpireAuction(), cancellationToken) :> Task

        member this.GetState(cancellationToken: CancellationToken) : Task<AuctionState> =
            Task.FromResult(this.CurrentState)

        member this.GetData(cancellationToken: CancellationToken) : Task<AuctionData> =
            Task.FromResult(this.Data)

[<DaprActor("BadAuction")>]
type BadAuctionActor(context: ActorActivationContext, timers: IActorTimerScheduler) =
    inherit StateMachineActor<AuctionState, AuctionData>(context, timers, "BadAuction", AuctionData.Empty)

    override this.Configure(sm: IStateMachine<AuctionState, AuctionData>) =
        sm.InitialState(AuctionState.Open) |> ignore
        sm.In(AuctionState.Open).On<Bid>().When(fun _ bid -> bid.Amount > 0m).GoTo(AuctionState.Sold) |> ignore
        sm.In(AuctionState.Sold) |> ignore
        sm.In(AuctionState.Expired) |> ignore

    interface IAuctionActor with
        member this.PlaceBid(bid: Bid, cancellationToken: CancellationToken) : Task<BidResult> =
            this.Raise<BidResult>(bid, cancellationToken)

        member this.Close(cancellationToken: CancellationToken) : Task =
            Task.CompletedTask

        member this.Expire(cancellationToken: CancellationToken) : Task =
            Task.CompletedTask

        member this.GetState(cancellationToken: CancellationToken) : Task<AuctionState> =
            Task.FromResult(this.CurrentState)

        member this.GetData(cancellationToken: CancellationToken) : Task<AuctionData> =
            Task.FromResult(this.Data)