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
