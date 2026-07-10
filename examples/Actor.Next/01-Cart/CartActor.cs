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
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.Examples.Cart;

[GenerateActorClient]
public interface ICartActor : IActor
{
    Task AddItem(CartItem item, CancellationToken cancellationToken = default);

    Task<CartSummary> GetSummary(CancellationToken cancellationToken = default);

    Task AbandonCart(CancellationToken cancellationToken = default);
}

public sealed record CartItem(string Sku, int Quantity);

public sealed record CartSummary(int ItemCount, decimal Total, bool Abandoned);

public interface IPricingClient
{
    ValueTask<decimal> GetPriceAsync(string sku, CancellationToken cancellationToken = default);
}

public sealed class CartState
{
    public Dictionary<string, int> Items { get; set; } = [];

    public Dictionary<string, decimal> Prices { get; set; } = [];

    public bool Abandoned { get; set; }
}

[DaprActor("Cart")]
public sealed class CartActor(
    ActorActivationContext context,
    IPricingClient pricing,
    IActorTimerScheduler timers) : Actor, ICartActor
{
    private static readonly TimeSpan AbandonAfter = TimeSpan.FromMinutes(20);

    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task AddItem(CartItem item, CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("cart", () => new CartState(), cancellationToken);
        state.Value.Items[item.Sku] = state.Value.Items.GetValueOrDefault(item.Sku) + item.Quantity;
        state.Value.Prices[item.Sku] = await pricing.GetPriceAsync(item.Sku, cancellationToken);
        state.Value.Abandoned = false;

        await timers.RescheduleAsync("Cart", Id, "abandon-cart", AbandonAfter, nameof(AbandonCart), string.Empty, cancellationToken: cancellationToken);
    }

    public async Task<CartSummary> GetSummary(CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("cart", () => new CartState(), cancellationToken);
        var itemCount = state.Value.Items.Values.Sum();
        var total = state.Value.Items.Sum(item => item.Value * state.Value.Prices.GetValueOrDefault(item.Key));
        return new CartSummary(itemCount, total, state.Value.Abandoned);
    }

    public async Task AbandonCart(CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("cart", () => new CartState(), cancellationToken);
        state.Value.Abandoned = true;
    }
}
