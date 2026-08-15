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
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Examples.Store.Tests;

public sealed class PerTypeOptionsTests
{
    [Fact]
    public async Task Session_and_inventory_actors_run_in_process()
    {
        await using var runtime = CreateRuntime();
        var session = runtime.CreateActor<ICheckoutSessionActor>(ActorId.Create("session-1"), "CheckoutSession");
        var inventory = runtime.CreateActor<IInventoryActor>(ActorId.Create("sku-1"), "Inventory");

        var add = session.AddItem(new SessionItem("sku-1", 2));
        var restock = inventory.Adjust(new StockAdjustment(10, "restock"));
        await runtime.RunToIdle();
        await add;
        Assert.Equal(10, await restock);

        var summaryTask = session.GetSummary();
        var onHandTask = inventory.GetOnHand();
        await runtime.RunToIdle();

        var summary = await summaryTask;
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(["sku-1"], summary.Skus);
        Assert.Equal(10, await onHandTask);
    }

    [Fact]
    public void Per_type_overrides_reach_the_runtime_registrations()
    {
        _ = typeof(CheckoutSessionActor);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprActors(ConfigureStoreActors);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var registry = provider.GetRequiredService<ActorRuntimeRegistry>();

        var session = registry.GetByActorType("CheckoutSession").TypeOptions;
        Assert.NotNull(session);
        Assert.Equal(TimeSpan.FromMinutes(5), session!.IdleTimeout);
        Assert.True(session.DisableStateMigration);
        Assert.Null(session.DrainOngoingCallTimeout);
        Assert.Null(session.DrainRebalancedActors);
        Assert.Null(session.EnableReentrancy);
        Assert.Null(session.MaxReentrantDepth);

        var inventory = registry.GetByActorType("Inventory").TypeOptions;
        Assert.NotNull(inventory);
        Assert.Equal(TimeSpan.FromHours(2), inventory!.IdleTimeout);
        Assert.True(inventory.EnableReentrancy);
        Assert.Equal(4, inventory.MaxReentrantDepth);
        // DrainRebalancedActorsTimeout aliases DrainOngoingCallTimeout, so both report the override.
        Assert.Equal(TimeSpan.FromSeconds(5), inventory.DrainRebalancedActorsTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), inventory.DrainOngoingCallTimeout);
        Assert.Null(inventory.DrainRebalancedActors);
        Assert.Null(inventory.DisableStateMigration);
    }

    private static ActorTestRuntime CreateRuntime()
    {
        _ = typeof(CheckoutSessionActor);
        return new ActorTestRuntime(services => services.AddDaprActors(ConfigureStoreActors));
    }

    // Mirrors the configuration at the top of the sample's Program.cs.
    private static void ConfigureStoreActors(DaprActorsOptions options)
    {
        options.ActorIdleTimeout = TimeSpan.FromMinutes(30);
        options.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(10);

        options.Actors.RegisterActor<CheckoutSessionActor>(o =>
        {
            o.IdleTimeout = TimeSpan.FromMinutes(5);
            o.DisableStateMigration = true;
        });

        options.Actors.RegisterActor<InventoryActor>(o =>
        {
            o.IdleTimeout = TimeSpan.FromHours(2);
            o.EnableReentrancy = true;
            o.MaxReentrantDepth = 4;
            o.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(5);
        });
    }
}
