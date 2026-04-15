// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// A no-op state manager used for unit testing.
/// </summary>
internal sealed class NoOpActorStateManager : IActorStateManager
{
    /// <inheritdoc />
    public Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default) =>
        Task.FromResult<T>(default!);

    /// <inheritdoc />
    public Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConditionalValue<T>.None);

    /// <inheritdoc />
    public Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <inheritdoc />
    public Task SaveStateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task ClearCacheAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
