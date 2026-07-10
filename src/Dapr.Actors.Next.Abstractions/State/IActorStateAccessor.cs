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

namespace Dapr.Actors.Next.Abstractions.State;

/// <summary>
/// Accesses typed actor state through the runtime-owned state cache.
/// </summary>
public interface IActorStateAccessor
{
    /// <summary>
    /// Gets state by name.
    /// </summary>
    ValueTask<IActorState<T>?> TryGetAsync<T>(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets existing state or creates a new value.
    /// </summary>
    ValueTask<IActorState<T>> GetOrCreateAsync<T>(string name, Func<T> valueFactory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets state by name.
    /// </summary>
    ValueTask SetAsync<T>(string name, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Folds state to the requested type and writes it without a migration discriminator.
    /// </summary>
    ValueTask GraduateAsync<T>(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes state by name.
    /// </summary>
    ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists pending state changes immediately instead of waiting for the end-of-turn save,
    /// then marks the saved cache entries as clean.
    /// </summary>
    ValueTask SaveStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads cached state entries without persisting them so future state operations read from durable state again.
    /// </summary>
    ValueTask EvictCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads cached state entries without persisting them so future state operations read from durable state again.
    /// </summary>
    ValueTask EvictCacheAsync(DaprEvictStateOptions options, CancellationToken cancellationToken = default);
}
