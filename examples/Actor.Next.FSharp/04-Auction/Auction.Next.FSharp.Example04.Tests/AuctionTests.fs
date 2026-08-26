namespace Auction.Next.FSharp.Example04.Tests

open System
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Options
open Dapr.Actors.Next.Testing
open Microsoft.Extensions.DependencyInjection
open Auction.Next.FSharp.Example04
open Xunit

type AuctionTests() =

    static member private CreateRuntime(scheduler: ControlledActorScheduler) =
        // Force-load the Glue assembly and invoke its generated registration module
        // (the module initializer does not fire for LoadFrom in this runtime version)
        let gluePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Auction.Next.FSharp.Example04.Glue.dll")
        let glueAssembly = System.Reflection.Assembly.LoadFrom(gluePath)
        match glueAssembly.GetType("Dapr.Actors.Next.Generated.GeneratedActorRegistrationModule") with
        | null -> failwith "GeneratedActorRegistrationModule not found in Glue assembly"
        | moduleType ->
            match moduleType.GetMethod("Register", System.Reflection.BindingFlags.Static ||| System.Reflection.BindingFlags.NonPublic) with
            | null -> failwith "Register method not found"
            | registerMethod -> registerMethod.Invoke(null, null) |> ignore
        // Force the F# types to be loaded so the generated registry discovers them
        let _ = typeof<AuctionActor>
        let opts = ActorTestRuntimeOptions()
        opts.Scheduler <- scheduler
        ActorTestRuntime(
            (fun services -> services.AddDaprActors(Action<DaprActorsOptions>(fun _ -> ())) |> ignore),
            opts)

    static member private PlaceBid(runtime: ActorTestRuntime, auction: IAuctionActor, bid: Bid) = task {
        let placed = auction.PlaceBid(bid, CancellationToken.None)
        do! runtime.RunToIdle()
        return! placed
    }

    static member private ReadState(runtime: ActorTestRuntime, auction: IAuctionActor) = task {
        let read = auction.GetState(CancellationToken.None)
        do! runtime.RunToIdle()
        return! read
    }

    static member private ReadData(runtime: ActorTestRuntime, auction: IAuctionActor) = task {
        let read = auction.GetData(CancellationToken.None)
        do! runtime.RunToIdle()
        return! read
    }

    [<Fact>]
    member this.Last_second_bid_wins_race_with_close_timer() = task {
        use runtime = AuctionTests.CreateRuntime(PriorityActorScheduler(7))
        let auction = runtime.CreateActor<IAuctionActor>(ActorId.Create("auction-1"), "Auction")
        let! _ = AuctionTests.PlaceBid(runtime, auction, { Amount = 100m; Bidder = "alice" })

        runtime.Time.Advance(TimeSpan.FromSeconds(29.0))
        let lateBid = auction.PlaceBid({ Amount = 110m; Bidder = "bob" }, CancellationToken.None)
        do! runtime.RunToIdle()
        let! result = lateBid
        Assert.Equal(BidResult.Accepted, result)

        runtime.Time.Advance(TimeSpan.FromSeconds(2.0))
        do! runtime.RunToIdle()

        let! state = AuctionTests.ReadState(runtime, auction)
        Assert.Equal(AuctionState.Open, state)
        let! data = AuctionTests.ReadData(runtime, auction)
        Assert.Equal("bob", data.HighBidder)
    }

    [<Fact>]
    member this.State_machine_has_no_structural_defects() =
        ActorStateMachine.Analyze<AuctionActor>().AssertNoStructuralDefects()
        let defects = ActorStateMachine.Analyze<BadAuctionActor>().StructuralDefects
        Assert.Contains(defects, fun defect -> defect.Contains("unreachable", StringComparison.Ordinal))
        Assert.False(CoyoteBridge.IsEnabled)