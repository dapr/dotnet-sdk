using System.Reflection;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Testing;

namespace Dapr.Actors.Next.Examples.Migration.Tests;

public sealed class MigrationTests
{
    [Fact]
    public async Task Old_v1_shape_is_upcasted_to_current_state()
    {
        await using var runtime = CreateRuntime();
        await SeedState(runtime, "cart-v1", new ActorStateEnvelope<CartStateV1>(1, new CartStateV1 { Skus = ["sku-1", "sku-1", "sku-2"] }));
        var cart = runtime.CreateActor<IMigratingCartActor>(ActorId.Create("cart-v1"), "MigratingCart");

        var read = cart.GetCurrentState();
        await runtime.RunToIdle();
        var current = await read;

        Assert.Equal(2, current.Lines.Single(line => line.Sku == "sku-1").Quantity);
        Assert.Equal(3, current.TotalQuantity);
        Assert.Equal(3, runtime.StateOf(cart).Get<CartStateV3>("cart")!.TotalQuantity);
    }

    [Fact]
    public async Task V1_to_v2_to_v3_chain_folds_in_one_activation()
    {
        await using var runtime = CreateRuntime();
        await SeedState(runtime, "cart-chain", new ActorStateEnvelope<CartStateV1>(1, new CartStateV1 { Skus = ["sku-1", "sku-2", "sku-2"] }));
        var cart = runtime.CreateActor<IMigratingCartActor>(ActorId.Create("cart-chain"), "MigratingCart");

        var read = cart.GetCurrentState();
        await runtime.RunToIdle();
        var current = await read;

        Assert.Equal(2, current.Lines.Single(line => line.Sku == "sku-2").Quantity);
        Assert.Equal(3, current.TotalQuantity);
    }

    [Fact]
    public async Task Imported_legacy_state_can_be_read_after_migration()
    {
        await using var runtime = CreateRuntime();
        var cart = runtime.CreateActor<IMigratingCartActor>(ActorId.Create("cart-local"), "MigratingCart");

        var import = cart.ImportLegacyV1(new CartStateV1 { Skus = ["sku-1", "sku-1", "sku-2"] });
        await runtime.RunToIdle();
        var imported = await import;

        var read = cart.GetCurrentState();
        await runtime.RunToIdle();
        var current = await read;

        Assert.Equal(3, imported.TotalQuantity);
        Assert.Equal(2, current.Lines.Single(line => line.Sku == "sku-1").Quantity);
        Assert.Equal(3, current.TotalQuantity);
    }

    private static ActorTestRuntime CreateRuntime()
    {
        _ = typeof(MigratingCartActor);
        return new ActorTestRuntime(services => services.AddDaprActors(_ => { }));
    }

    private static async ValueTask SeedState<T>(ActorTestRuntime runtime, string actorId, ActorStateEnvelope<T> envelope)
    {
        var store = runtime.GetType().GetProperty("StateStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var write = store.GetType().GetMethod("WriteAsync")!;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var result = (ValueTask)write.Invoke(store, ["MigratingCart", actorId, "cart", new ReadOnlyMemory<byte>(bytes), CancellationToken.None])!;
        await result;
    }
}
