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

using System.Text;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.StateMachine.Test;

public sealed class StateMachineTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Auction_runs_end_to_end_and_queries_bypass_machine()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("auction-1");

        Assert.Equal(StartResult.Started, await Invoke<StartResult>(runtime, id, "Start"));
        Assert.Equal(BidResult.Accepted, await Invoke<BidResult>(runtime, id, "PlaceBid", new Bid(100, "alice")));
        Assert.Equal(BidResult.TooLow, await Invoke<BidResult>(runtime, id, "PlaceBid", new Bid(99, "bob")));

        var data = await Invoke<AuctionData>(runtime, id, "ReadData");
        Assert.Equal(100, data.HighBid);
        Assert.Equal("alice", data.HighBidder);
        Assert.Equal(1, data.OpenEntries);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Run_to_completion_processes_internal_timer_stability_before_next_turn()
    {
        await using var runtime = CreateRuntime(new SeededRandomActorScheduler(2));
        var id = ActorId.Create("rtc");
        var start = Invoke<StartResult>(runtime, id, "Start");
        var bid = Invoke<BidResult>(runtime, id, "PlaceBid", new Bid(100, "alice"));
        await runtime.RunToIdle();

        Assert.Equal(StartResult.Started, await start);
        Assert.Equal(BidResult.Accepted, await bid);
        var data = await Invoke<AuctionData>(runtime, id, "ReadData");
        Assert.Equal(["entry-open", "bid:alice"], data.Log);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Rehydration_restores_state_without_entry_actions()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("rehydrate");
        Assert.Equal(StartResult.Started, await Invoke<StartResult>(runtime, id, "Start"));
        Assert.Equal(1, (await Invoke<AuctionData>(runtime, id, "ReadData")).OpenEntries);

        var deactivate = runtime.InvokeAsync("Auction", id, "Deactivate", kind: ActorTurnKind.Deactivate);
        await runtime.RunToIdle();
        _ = await deactivate;
        Assert.Equal(AuctionState.Open, await Invoke<AuctionState>(runtime, id, "ReadState"));
        Assert.Equal(1, (await Invoke<AuctionData>(runtime, id, "ReadData")).OpenEntries);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Durable_defer_round_trips_through_state_and_replays_after_transition()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("defer");
        var ack = await Invoke<DeferredEventAck>(runtime, id, "PlaceDeferredBid", new Bid(50, "alice"));
        Assert.True(ack.Deferred);

        Assert.Equal(StartResult.Started, await Invoke<StartResult>(runtime, id, "Start"));
        var data = await Invoke<AuctionData>(runtime, id, "ReadData");
        Assert.Equal(50, data.HighBid);
        Assert.Equal("alice", data.HighBidder);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Hierarchy_bubbles_child_unhandled_event_to_parent()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("hierarchy");
        Assert.Equal(StartResult.Started, await Invoke<StartResult>(runtime, id, "Start"));
        await Invoke<object?>(runtime, id, "Pause");
        Assert.Equal(AuctionState.Paused, await Invoke<AuctionState>(runtime, id, "ReadState"));
        await Invoke<object?>(runtime, id, "Resume");
        Assert.Equal(AuctionState.Open, await Invoke<AuctionState>(runtime, id, "ReadState"));
        await Invoke<object?>(runtime, id, "Cancel");

        Assert.Equal(AuctionState.Expired, await Invoke<AuctionState>(runtime, id, "ReadState"));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Ignore_differs_from_unhandled()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("ignore");
        await Invoke<StartResult>(runtime, id, "Start");
        runtime.Time.Advance(TimeSpan.FromSeconds(30));
        await runtime.RunToIdle();
        Assert.Equal(AuctionState.Sold, await Invoke<AuctionState>(runtime, id, "ReadState"));

        Assert.Equal(default, await Invoke<BidResult>(runtime, id, "PlaceBid", new Bid(200, "late")));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke<object?>(runtime, ActorId.Create("bad"), "Cancel"));
        Assert.Contains("Unhandled", ex.Message);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Declarative_timeout_uses_virtual_time()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("timeout");
        await Invoke<StartResult>(runtime, id, "Start");
        runtime.Time.Advance(TimeSpan.FromSeconds(29));
        await runtime.RunToIdle();
        Assert.Equal(AuctionState.Open, await Invoke<AuctionState>(runtime, id, "ReadState"));

        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(AuctionState.Sold, await Invoke<AuctionState>(runtime, id, "ReadState"));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Last_second_bid_extends_soft_close_and_original_timer_is_not_lost()
    {
        await using var runtime = CreateRuntime(new PriorityActorScheduler(7));
        var id = ActorId.Create("race");
        await Invoke<StartResult>(runtime, id, "Start");
        await Invoke<BidResult>(runtime, id, "PlaceBid", new Bid(100, "alice"));

        runtime.Time.Advance(TimeSpan.FromSeconds(29));
        var late = Invoke<BidResult>(runtime, id, "PlaceBid", new Bid(110, "bob"));
        runtime.Time.Advance(TimeSpan.FromSeconds(2));
        await runtime.RunToIdle();

        Assert.Equal(BidResult.Accepted, await late);
        Assert.Equal(AuctionState.Open, await Invoke<AuctionState>(runtime, id, "ReadState"));
        Assert.Equal("bob", (await Invoke<AuctionData>(runtime, id, "ReadData")).HighBidder);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Structural_analysis_reports_broken_machine_defects()
    {
        ActorStateMachine.Analyze<AuctionActor>().AssertNoStructuralDefects();
        var broken = ActorStateMachine.Analyze<BrokenActor>();

        Assert.Contains(broken.StructuralDefects, defect => defect.Contains("guard chain", StringComparison.Ordinal));
        Assert.Contains(broken.StructuralDefects, defect => defect.Contains("unreachable", StringComparison.Ordinal));
        Assert.Contains(broken.StructuralDefects, defect => defect.Contains("dead end", StringComparison.Ordinal));
    }

    private static ActorTestRuntime CreateRuntime(ControlledActorScheduler? scheduler = null) =>
        new(services => services.AddDaprActorsCore(registrations =>
        {
            registrations.Add(
                "Auction",
                typeof(IAuctionActor),
                typeof(AuctionActor),
                (sp, _) => new AuctionActor(sp.GetRequiredService<ActorActivationContext>(), sp.GetRequiredService<IActorTimerScheduler>()),
                new AuctionActorDispatcher(),
                new ActorLifecycle(
                    (actor, cancellationToken) => ((Actor)actor).InvokeOnActivateAsync(cancellationToken),
                    (actor, cancellationToken) => ((Actor)actor).InvokeOnDeactivateAsync(cancellationToken),
                    (actor, context, cancellationToken) => ((Actor)actor).InvokeOnPreActorMethodAsync(context, cancellationToken),
                    (actor, context, exception, cancellationToken) => ((Actor)actor).InvokeOnPostActorMethodAsync(context, exception, cancellationToken)));
        }), new ActorTestRuntimeOptions { Scheduler = scheduler });

    private static async Task<T> Invoke<T>(ActorTestRuntime runtime, ActorId id, string operation, object? value = null)
    {
        var payload = value is null ? string.Empty : JsonSerializer.Serialize(value);
        var call = runtime.InvokeAsync("Auction", id, operation, payload);
        await runtime.RunToIdle();
        var bytes = await call;
        if (bytes is null || bytes.Length == 0)
        {
            return default!;
        }

        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(bytes))!;
    }
}
