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

using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.StateMachine.Test;

public enum AuctionState
{
    Scheduled,
    Live,
    Open,
    Paused,
    Sold,
    Expired,
}

public sealed record AuctionData(decimal HighBid, string? HighBidder, int OpenEntries, int OpenExits, string[] Log)
{
    public static AuctionData Empty { get; } = new(0, null, 0, 0, []);
}

public sealed record Bid(decimal Amount, string Bidder);

public sealed record StartAuction;

public sealed record PauseAuction;

public sealed record ResumeAuction;

public sealed record CancelAuction;

public enum BidResult
{
    Accepted,
    TooLow,
}

public enum StartResult
{
    Started,
}

public interface IAuctionActor : IActor;

public sealed class AuctionActor(ActorActivationContext context, IActorTimerScheduler timerScheduler) :
    StateMachineActor<AuctionState, AuctionData>(context, timerScheduler, "Auction", AuctionData.Empty),
    IAuctionActor
{
    protected override void Configure(IStateMachine<AuctionState, AuctionData> sm)
    {
        sm.InitialState(AuctionState.Scheduled);

        sm.In(AuctionState.Scheduled)
            .Defer<Bid>();

        sm.In(AuctionState.Scheduled)
            .On<StartAuction>().GoTo(AuctionState.Open).Reply(StartResult.Started);

        sm.In(AuctionState.Live)
            .On<CancelAuction>().GoTo(AuctionState.Expired);

        sm.In(AuctionState.Open)
            .SubstateOf(AuctionState.Live)
            .OnEntry(ctx => ctx.Update(data => data with { OpenEntries = data.OpenEntries + 1, Log = [.. data.Log, "entry-open"] }))
            .OnExit(ctx => ctx.Update(data => data with { OpenExits = data.OpenExits + 1, Log = [.. data.Log, "exit-open"] }))
            .After(TimeSpan.FromSeconds(30))
            .On<Bid>()
                .When((data, bid) => bid.Amount > data.HighBid)
                    .Do(ctx =>
                    {
                        ctx.Update(data => data with { HighBid = ctx.Event.Amount, HighBidder = ctx.Event.Bidder, Log = [.. data.Log, $"bid:{ctx.Event.Bidder}"] });
                        ctx.Timers.Reschedule(StateMachineConstants.StateTimeoutTimerName, TimeSpan.FromSeconds(30));
                        ctx.Reply(BidResult.Accepted);
                    })
                .Otherwise()
                    .Reply(BidResult.TooLow);

        sm.In(AuctionState.Open)
            .On<PauseAuction>().GoTo(AuctionState.Paused);

        sm.In(AuctionState.Open)
            .On<StateTimeout<AuctionState>>().GoTo(AuctionState.Sold);

        sm.In(AuctionState.Paused)
            .SubstateOf(AuctionState.Live)
            .On<ResumeAuction>().GoTo(AuctionState.Open);

        sm.In(AuctionState.Paused)
            .Ignore<Bid>();

        sm.In(AuctionState.Sold)
            .OnEntry(ctx => ctx.Update(data => data with { Log = [.. data.Log, "sold"] }))
            .Ignore<Bid>();

        sm.In(AuctionState.Expired)
            .Ignore<Bid>();

        sm.OnUnhandled(ctx => throw new InvalidOperationException($"Unhandled {ctx.Event.GetType().Name} in {ctx.State}."));
    }

    public Task<StartResult> Start(CancellationToken cancellationToken = default) => Raise<StartResult>(new StartAuction(), cancellationToken);

    public Task<BidResult> PlaceBid(Bid bid, CancellationToken cancellationToken = default) => Raise<BidResult>(bid, cancellationToken);

    public Task<DeferredEventAck> PlaceDeferredBid(Bid bid, CancellationToken cancellationToken = default) => Raise<DeferredEventAck>(bid, cancellationToken);

    public Task Cancel(CancellationToken cancellationToken = default) => Raise<object?>(new CancelAuction(), cancellationToken);

    public Task Pause(CancellationToken cancellationToken = default) => Raise<object?>(new PauseAuction(), cancellationToken);

    public Task Resume(CancellationToken cancellationToken = default) => Raise<object?>(new ResumeAuction(), cancellationToken);

    public Task<AuctionState> ReadState(CancellationToken cancellationToken = default) => Task.FromResult(CurrentState);

    public Task<AuctionData> ReadData(CancellationToken cancellationToken = default) => Task.FromResult(Data);
}

public sealed class AuctionActorDispatcher : IActorDispatcher
{
    public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
    {
        var auction = (AuctionActor)actor;
        if (request.MethodName == StateMachineConstants.TimerOperationName)
        {
            await auction.DispatchStateMachineTimerAsync(System.Text.Encoding.UTF8.GetString(request.Payload.Span), cancellationToken);
            return new ActorDispatchResponse(null);
        }

        return request.MethodName switch
        {
            "Start" => Json(await auction.Start(cancellationToken)),
            "PlaceBid" => Json(await auction.PlaceBid(JsonSerializer.Deserialize<Bid>(request.Payload.Span)!, cancellationToken)),
            "PlaceDeferredBid" => Json(await auction.PlaceDeferredBid(JsonSerializer.Deserialize<Bid>(request.Payload.Span)!, cancellationToken)),
            "Cancel" => await CancelAsync(auction, cancellationToken),
            "Pause" => await PauseAsync(auction, cancellationToken),
            "Resume" => await ResumeAsync(auction, cancellationToken),
            "ReadState" => Json(await auction.ReadState(cancellationToken)),
            "ReadData" => Json(await auction.ReadData(cancellationToken)),
            _ => throw new InvalidOperationException($"Unknown operation '{request.MethodName}'."),
        };
    }

    private static async Task<ActorDispatchResponse> CancelAsync(AuctionActor auction, CancellationToken cancellationToken)
    {
        await auction.Cancel(cancellationToken);
        return new ActorDispatchResponse(null);
    }

    private static async Task<ActorDispatchResponse> PauseAsync(AuctionActor auction, CancellationToken cancellationToken)
    {
        await auction.Pause(cancellationToken);
        return new ActorDispatchResponse(null);
    }

    private static async Task<ActorDispatchResponse> ResumeAsync(AuctionActor auction, CancellationToken cancellationToken)
    {
        await auction.Resume(cancellationToken);
        return new ActorDispatchResponse(null);
    }

    private static ActorDispatchResponse Json<T>(T value) => new(JsonSerializer.SerializeToUtf8Bytes(value));
}
