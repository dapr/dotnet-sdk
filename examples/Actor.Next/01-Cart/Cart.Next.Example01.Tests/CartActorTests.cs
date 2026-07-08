using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Examples.Cart.Tests;

public sealed class CartActorTests
{
    [Fact]
    public async Task Adding_an_item_updates_the_summary()
    {
        await using var runtime = CreateRuntime();
        var cart = runtime.CreateActor<ICartActor>(ActorId.Create("cart-1"), "Cart");

        var add = cart.AddItem(new CartItem("sku-1", 2));
        await runtime.RunToIdle();
        await add;

        var read = cart.GetSummary();
        await runtime.RunToIdle();
        var summary = await read;

        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(25.00m, summary.Total);
        Assert.False(summary.Abandoned);
    }

    [Fact]
    public async Task Advancing_virtual_time_fires_the_abandon_timer()
    {
        await using var runtime = CreateRuntime();
        var cart = runtime.CreateActor<ICartActor>(ActorId.Create("idle-cart"), "Cart");

        var add = cart.AddItem(new CartItem("sku-1", 1));
        await runtime.RunToIdle();
        await add;

        runtime.Time.Advance(TimeSpan.FromMinutes(20));
        await runtime.RunToIdle();

        var summaryTask = cart.GetSummary();
        await runtime.RunToIdle();
        Assert.True((await summaryTask).Abandoned);
    }

    private static ActorTestRuntime CreateRuntime()
    {
        _ = typeof(CartActor);
        return new ActorTestRuntime(services =>
        {
            services.AddSingleton<IPricingClient, FakePricingClient>();
            services.AddDaprActors(_ => { });
        });
    }

    private sealed class FakePricingClient : IPricingClient
    {
        public ValueTask<decimal> GetPriceAsync(string sku, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(sku == "sku-1" ? 12.50m : 1.00m);
    }
}
