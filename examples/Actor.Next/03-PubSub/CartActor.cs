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
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.Examples.PubSub;

// The source generator uses this interface to create the strongly typed actor proxy used by Program.cs.
[GenerateActorClient]
public interface IRestockingCartActor : IActor
{
    Task AddUnavailableSku(string sku, CancellationToken cancellationToken = default);

    Task OnRestock(RestockEvent evt, CancellationToken cancellationToken = default);

    Task<bool> IsAvailable(string sku, CancellationToken cancellationToken = default);

    Task<RestockingCartState> GetState(CancellationToken cancellationToken = default);

    Task Clear(CancellationToken cancellationToken = default);
}

// The actor type name is part of Dapr's addressing model. It must match the name used
// by actor proxies and stream subscription routing.
[DaprActor(RestockingCartNames.ActorType)]
public sealed class RestockingCartActor(ActorActivationContext context) : Actor, IRestockingCartActor
{
    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task AddUnavailableSku(string sku, CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("cart", () => new RestockingCartState(), cancellationToken);
        state.Value.WaitingForStock.Add(sku);
        state.Value.Available.Remove(sku);
    }

    // Subscribe connects this actor method to a Dapr pub/sub topic. RouteBy tells the
    // stream runner which event property contains the target actor id.
    [Subscribe(RestockingCartNames.PubsubName, RestockingCartNames.RestockTopic, RouteBy = nameof(RestockEvent.CartId))]
    public async Task OnRestock(RestockEvent evt, CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("cart", () => new RestockingCartState(), cancellationToken);

        // Pub/sub delivery is at-least-once, so this method must be safe to run more than once.
        // Removing first makes duplicate deliveries no-ops after the first successful turn.
        if (state.Value.WaitingForStock.Remove(evt.Sku))
        {
            state.Value.Available.Add(evt.Sku);
        }
    }

    public async Task<bool> IsAvailable(string sku, CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("cart", () => new RestockingCartState(), cancellationToken);
        return state.Value.Available.Contains(sku);
    }

    public async Task<RestockingCartState> GetState(CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("cart", () => new RestockingCartState(), cancellationToken);
        return new RestockingCartState
        {
            WaitingForStock = [.. state.Value.WaitingForStock],
            Available = [.. state.Value.Available],
        };
    }

    public async Task Clear(CancellationToken cancellationToken = default) =>
        await State.RemoveAsync("cart", cancellationToken);
}
