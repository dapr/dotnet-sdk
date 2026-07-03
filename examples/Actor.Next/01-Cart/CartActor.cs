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
