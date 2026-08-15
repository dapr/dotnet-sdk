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

namespace Dapr.Actors.Next.Examples.Store;

[GenerateActorClient]
public interface IInventoryActor : IActor
{
    Task<int> Adjust(StockAdjustment adjustment, CancellationToken cancellationToken = default);

    Task<int> GetOnHand(CancellationToken cancellationToken = default);
}

public sealed record StockAdjustment(int Quantity, string Reason);

public sealed class InventoryState
{
    public int OnHand { get; set; }
}

/// <summary>
/// A per-SKU inventory aggregate. Stock levels are read constantly, so the app
/// registers this type with a long per-type idle timeout to keep hot instances
/// resident, and enables reentrancy so inventory calls can call back into the
/// same chain (see Program.cs).
/// </summary>
[DaprActor("Inventory")]
public sealed class InventoryActor(ActorActivationContext context) : Actor, IInventoryActor
{
    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task<int> Adjust(StockAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("inventory", () => new InventoryState(), cancellationToken);
        state.Value.OnHand += adjustment.Quantity;
        return state.Value.OnHand;
    }

    public async Task<int> GetOnHand(CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("inventory", () => new InventoryState(), cancellationToken);
        return state.Value.OnHand;
    }
}
