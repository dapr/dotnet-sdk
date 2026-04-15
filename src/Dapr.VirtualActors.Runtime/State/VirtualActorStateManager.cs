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

namespace Dapr.VirtualActors.Runtime.State;

/// <summary>
/// Default <see cref="IActorStateManager"/> implementation that tracks state changes
/// in memory and flushes them transactionally via <see cref="IActorStateProvider"/>.
/// </summary>
internal sealed class VirtualActorStateManager : IActorStateManager
{
    private readonly string _actorType;
    private readonly VirtualActorId _actorId;
    private readonly IActorStateProvider _stateProvider;
    private readonly Dictionary<string, StateMetadata> _tracker = new(StringComparer.Ordinal);

    public VirtualActorStateManager(
        string actorType,
        VirtualActorId actorId,
        IActorStateProvider stateProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentNullException.ThrowIfNull(stateProvider);

        _actorType = actorType;
        _actorId = actorId;
        _stateProvider = stateProvider;
    }

    /// <inheritdoc />
    public async Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

        if (_tracker.TryGetValue(stateName, out var existing) && existing.ChangeKind != StateChangeKind.Remove)
        {
            throw new InvalidOperationException($"State with name '{stateName}' already exists. Use SetStateAsync to update.");
        }

        // Check underlying store if not tracked
        if (!_tracker.ContainsKey(stateName))
        {
            var result = await _stateProvider.TryLoadStateAsync<T>(_actorType, _actorId, stateName, cancellationToken);
            if (result.HasValue)
            {
                throw new InvalidOperationException($"State with name '{stateName}' already exists in the state store.");
            }
        }

        _tracker[stateName] = new StateMetadata(value, StateChangeKind.Add);
    }

    /// <inheritdoc />
    public async Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default)
    {
        var result = await TryGetStateAsync<T>(stateName, cancellationToken);
        return result.HasValue
            ? result.Value
            : throw new KeyNotFoundException($"State with name '{stateName}' does not exist.");
    }

    /// <inheritdoc />
    public async Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

        if (_tracker.TryGetValue(stateName, out var tracked))
        {
            return tracked.ChangeKind == StateChangeKind.Remove
                ? ConditionalValue<T>.None
                : ConditionalValue<T>.Some((T)tracked.Value!);
        }

        var result = await _stateProvider.TryLoadStateAsync<T>(_actorType, _actorId, stateName, cancellationToken);
        if (result.HasValue)
        {
            // Cache it locally
            _tracker[stateName] = new StateMetadata(result.Value, StateChangeKind.None);
        }

        return result;
    }

    /// <inheritdoc />
    public Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

        if (_tracker.TryGetValue(stateName, out var existing))
        {
            var kind = existing.ChangeKind == StateChangeKind.None
                ? StateChangeKind.Update
                : existing.ChangeKind; // Keep Add if it was Add

            _tracker[stateName] = new StateMetadata(value, kind);
        }
        else
        {
            _tracker[stateName] = new StateMetadata(value, StateChangeKind.Update);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

        if (!_tracker.ContainsKey(stateName))
        {
            // Must exist in store — we'll track it as a removal
            _tracker[stateName] = new StateMetadata(null, StateChangeKind.Remove);
        }
        else
        {
            _tracker[stateName] = new StateMetadata(null, StateChangeKind.Remove);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

        if (_tracker.TryGetValue(stateName, out var existing) && existing.ChangeKind == StateChangeKind.Remove)
        {
            return Task.FromResult(false); // Already removed
        }

        _tracker[stateName] = new StateMetadata(null, StateChangeKind.Remove);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

        if (_tracker.TryGetValue(stateName, out var tracked))
        {
            return tracked.ChangeKind != StateChangeKind.Remove;
        }

        var result = await _stateProvider.TryLoadStateAsync<object>(_actorType, _actorId, stateName, cancellationToken);
        return result.HasValue;
    }

    /// <inheritdoc />
    public async Task SaveStateAsync(CancellationToken cancellationToken = default)
    {
        var changes = _tracker
            .Where(kvp => kvp.Value.ChangeKind != StateChangeKind.None)
            .Select(kvp => new ActorStateChange(kvp.Key, kvp.Value.Value, kvp.Value.ChangeKind))
            .ToList();

        if (changes.Count > 0)
        {
            await _stateProvider.SaveStateAsync(_actorType, _actorId, changes, cancellationToken);
        }

        // Reset tracker — all changes persisted, keep cached values
        foreach (var key in _tracker.Keys.ToList())
        {
            var entry = _tracker[key];
            if (entry.ChangeKind == StateChangeKind.Remove)
            {
                _tracker.Remove(key);
            }
            else
            {
                _tracker[key] = entry with { ChangeKind = StateChangeKind.None };
            }
        }
    }

    /// <inheritdoc />
    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        _tracker.Clear();
        return Task.CompletedTask;
    }

    private sealed record StateMetadata(object? Value, StateChangeKind ChangeKind);
}
