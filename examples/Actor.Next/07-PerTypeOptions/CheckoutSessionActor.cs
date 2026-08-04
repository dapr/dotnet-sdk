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
public interface ICheckoutSessionActor : IActor
{
    Task AddItem(SessionItem item, CancellationToken cancellationToken = default);

    Task<SessionSummary> GetSummary(CancellationToken cancellationToken = default);
}

public sealed record SessionItem(string Sku, int Quantity);

public sealed record SessionSummary(int ItemCount, IReadOnlyList<string> Skus);

public sealed class CheckoutSessionState
{
    public Dictionary<string, int> Items { get; set; } = [];
}

/// <summary>
/// A short-lived checkout session. Sessions are abandoned often, so the app
/// registers this type with a short per-type idle timeout (see Program.cs)
/// and lets daprd deactivate idle instances quickly instead of holding them
/// for the app-wide idle window.
/// </summary>
[DaprActor("CheckoutSession")]
public sealed class CheckoutSessionActor(ActorActivationContext context) : Actor, ICheckoutSessionActor
{
    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task AddItem(SessionItem item, CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("session", () => new CheckoutSessionState(), cancellationToken);
        state.Value.Items[item.Sku] = state.Value.Items.GetValueOrDefault(item.Sku) + item.Quantity;
    }

    public async Task<SessionSummary> GetSummary(CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("session", () => new CheckoutSessionState(), cancellationToken);
        var skus = state.Value.Items.Keys.OrderBy(sku => sku, StringComparer.Ordinal).ToArray();
        return new SessionSummary(state.Value.Items.Values.Sum(), skus);
    }
}
