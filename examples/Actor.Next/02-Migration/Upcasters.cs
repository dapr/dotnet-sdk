using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Examples.Migration;

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

public sealed class RenamedStateToV2 : IActorStateUpcaster<RenamedState, RenamedStateV2>
{
    public ValueTask<RenamedStateV2> UpcastAsync(RenamedState state, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new RenamedStateV2
        {
            DisplayName = string.Join(' ',
                new[] { state.FirstName, state.LastName }.Where(static part => part.Length > 0)),
        });
}

public sealed class CartStateV2ToV3 : IActorStateUpcaster<CartStateV2, CartStateV3>
{
    public ValueTask<CartStateV3> UpcastAsync(CartStateV2 state, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new CartStateV3
        {
            Lines = state.Lines.ToList(), TotalQuantity = state.Lines.Sum(line => line.Quantity),
        });
}
