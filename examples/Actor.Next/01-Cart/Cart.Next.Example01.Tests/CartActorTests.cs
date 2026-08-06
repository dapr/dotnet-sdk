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
