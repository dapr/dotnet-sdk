using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.Examples.Migration;

[GenerateActorClient]
public interface IMigratingCartActor : IActor
{
    Task<CartStateV3> GetCurrentState(CancellationToken cancellationToken = default);

    Task<CartStateV3> ImportLegacyV1(CartStateV1 state, CancellationToken cancellationToken = default);

    Task<CartStateV3> ImportLegacyV2(CartStateV2 state, CancellationToken cancellationToken = default);

    Task Clear(CancellationToken cancellationToken = default);
}

public sealed record CartLine(string Sku, int Quantity);

public sealed class CartStateV1
{
    public List<string> Skus { get; init; } = [];
}

public sealed class CartStateV2
{
    public List<CartLine> Lines { get; init; } = [];
}

public sealed class CartStateV3
{
    public List<CartLine> Lines { get; init; } = [];

    public int TotalQuantity { get; init; }
}

public sealed class CartStateSnapshot
{
    public List<string> Skus { get; init; } = [];

    public List<CartLine> Lines { get; init; } = [];

    public int TotalQuantity { get; init; }
}

public sealed class CartStateV1ToV2 : IActorStateUpcaster<CartStateV1, CartStateV2>
{
    public ValueTask<CartStateV2> UpcastAsync(CartStateV1 state, CancellationToken cancellationToken = default)
    {
        var lines = state.Skus
            .GroupBy(sku => sku, StringComparer.Ordinal)
            .Select(group => new CartLine(group.Key, group.Count()))
            .ToList();

        return ValueTask.FromResult(new CartStateV2 { Lines = lines });
    }
}

public sealed class CartStateV2ToV3 : IActorStateUpcaster<CartStateV2, CartStateV3>
{
    public ValueTask<CartStateV3> UpcastAsync(CartStateV2 state, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new CartStateV3
        {
            Lines = state.Lines.ToList(),
            TotalQuantity = state.Lines.Sum(line => line.Quantity),
        });
}

[DaprActor("MigratingCart")]
public sealed class MigratingCartActor(
    ActorActivationContext context,
    IActorStateUpcaster<CartStateV1, CartStateV2> v1ToV2,
    IActorStateUpcaster<CartStateV2, CartStateV3> v2ToV3) : Actor, IMigratingCartActor
{
    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task<CartStateV3> GetCurrentState(CancellationToken cancellationToken = default)
    {
        var cart = await TryGetSnapshot(cancellationToken);
        if (cart is null)
        {
            var empty = new CartStateV3();
            await State.SetAsync("cart", empty, 3, cancellationToken);
            return empty;
        }

        if (cart.SchemaVersion == 1)
        {
            var v2 = await v1ToV2.UpcastAsync(new CartStateV1 { Skus = cart.Value.Skus }, cancellationToken);
            var v3 = await v2ToV3.UpcastAsync(v2, cancellationToken);
            await State.SetAsync("cart", v3, 3, cancellationToken);
            return v3;
        }

        if (cart.SchemaVersion == 2)
        {
            var v3 = await v2ToV3.UpcastAsync(new CartStateV2 { Lines = cart.Value.Lines }, cancellationToken);
            await State.SetAsync("cart", v3, 3, cancellationToken);
            return v3;
        }

        return new CartStateV3
        {
            Lines = cart.Value.Lines,
            TotalQuantity = cart.Value.TotalQuantity,
        };
    }

    public async Task<CartStateV3> ImportLegacyV1(CartStateV1 state, CancellationToken cancellationToken = default)
    {
        var v2 = await v1ToV2.UpcastAsync(state, cancellationToken);
        var v3 = await v2ToV3.UpcastAsync(v2, cancellationToken);
        await State.SetAsync("cart", v3, 3, cancellationToken);
        return v3;
    }

    public async Task<CartStateV3> ImportLegacyV2(CartStateV2 state, CancellationToken cancellationToken = default)
    {
        var v3 = await v2ToV3.UpcastAsync(state, cancellationToken);
        await State.SetAsync("cart", v3, 3, cancellationToken);
        return v3;
    }

    public async Task Clear(CancellationToken cancellationToken = default) =>
        await State.RemoveAsync("cart", cancellationToken);

    private async Task<IActorState<CartStateSnapshot>?> TryGetSnapshot(CancellationToken cancellationToken)
    {
        try
        {
            return await State.TryGetAsync<CartStateSnapshot>("cart", cancellationToken);
        }
        catch (InvalidCastException)
        {
            var current = await State.TryGetAsync<CartStateV3>("cart", cancellationToken);
            if (current is null)
            {
                return null;
            }

            return new SnapshotActorState("cart", current.SchemaVersion, new CartStateSnapshot
            {
                Lines = current.Value.Lines,
                TotalQuantity = current.Value.TotalQuantity,
            });
        }
    }

    private sealed class SnapshotActorState(string name, int schemaVersion, CartStateSnapshot value) : IActorState<CartStateSnapshot>
    {
        public string Name { get; } = name;

        public int SchemaVersion { get; } = schemaVersion;

        public CartStateSnapshot Value { get; set; } = value;
    }
}
