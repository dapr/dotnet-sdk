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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Testing;

namespace Dapr.Actors.Next.Examples.Auction.Tests;

public sealed class AuctionTests
{
    [Fact]
    public async Task Last_second_bid_wins_race_with_close_timer()
    {
        await using var runtime = CreateRuntime(new PriorityActorScheduler(7));
        var auction = runtime.CreateActor<IAuctionActor>(ActorId.Create("auction-1"), "Auction");
        await PlaceBid(runtime, auction, new Bid(100, "alice"));

        runtime.Time.Advance(TimeSpan.FromSeconds(29));
        var lateBid = auction.PlaceBid(new Bid(110, "bob"));
        await runtime.RunToIdle();
        Assert.Equal(BidResult.Accepted, await lateBid);

        runtime.Time.Advance(TimeSpan.FromSeconds(2));
        await runtime.RunToIdle();

        Assert.Equal(AuctionState.Open, await ReadState(runtime, auction));
        Assert.Equal("bob", (await ReadData(runtime, auction)).HighBidder);
    }

    [Fact]
    public void State_machine_has_no_structural_defects()
    {
        ActorStateMachine.Analyze<AuctionActor>().AssertNoStructuralDefects();
        Assert.Contains(ActorStateMachine.Analyze<BadAuctionActor>().StructuralDefects, defect => defect.Contains("unreachable", StringComparison.Ordinal));

        // For deeper exploration, run the same test body under the optional Coyote bridge when enabled.
        Assert.False(CoyoteBridge.IsEnabled);
    }

    private static ActorTestRuntime CreateRuntime(ControlledActorScheduler? scheduler = null)
    {
        _ = typeof(AuctionActor);
        return new ActorTestRuntime(services => services.AddDaprActors(_ => { }), new ActorTestRuntimeOptions { Scheduler = scheduler });
    }

    private static async Task<BidResult> PlaceBid(ActorTestRuntime runtime, IAuctionActor auction, Bid bid)
    {
        var placed = auction.PlaceBid(bid);
        await runtime.RunToIdle();
        return await placed;
    }

    private static async Task<AuctionState> ReadState(ActorTestRuntime runtime, IAuctionActor auction)
    {
        var read = auction.GetState();
        await runtime.RunToIdle();
        return await read;
    }

    private static async Task<AuctionData> ReadData(ActorTestRuntime runtime, IAuctionActor auction)
    {
        var read = auction.GetData();
        await runtime.RunToIdle();
        return await read;
    }
}
