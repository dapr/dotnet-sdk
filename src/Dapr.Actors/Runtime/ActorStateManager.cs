// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Resources;
using Dapr.Actors.Communication;

namespace Dapr.Actors.Runtime;

internal sealed class ActorStateManager : IActorStateManager, IActorContextualState
{
    private readonly Actor actor;
    private readonly string actorTypeName;
    private readonly IActorStateCache defaultCache;
    private static readonly AsyncLocal<(string id, IActorStateCache stateCache)> context = new();
        
    internal ActorStateManager(Actor actor)
    {
        this.actor = actor;
        this.actorTypeName = actor.Host.ActorTypeInfo.ActorTypeName;
        this.defaultCache =  new ActorStateCache();
    }

    internal ActorStateManager(Actor actor, IActorStateCache stateCache)
    {
        this.actor = actor;
        this.actorTypeName = actor.Host.ActorTypeInfo.ActorTypeName;
        this.defaultCache = stateCache;
    }

    public async Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();

        if (!(await this.TryAddStateAsync(stateName, value, cancellationToken)))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, SR.ActorStateAlreadyExists, stateName));
        }
    }

    public async Task AddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();

        if (!(await this.TryAddStateAsync(stateName, value, ttl, cancellationToken)))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, SR.ActorStateAlreadyExists, stateName));
        }
    }

    public async Task<bool> TryAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var cache = GetContextualStateTracker();
        var (stateContainsKey, addedToState) = cache.Add(stateName, value);
        if (stateContainsKey)
        {
            return addedToState;
        }

        if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            return false;
        }

        return addedToState;
    }

    public async Task<bool> TryAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var cache = GetContextualStateTracker();
        var (stateContainsKey, addedToState) = cache.Add(stateName, value, ttl);
        if (stateContainsKey)
        {
            return addedToState;
        }
        
        if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            return false;
        }

        return addedToState;
    }

    public async Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();

        var condRes = await this.TryGetStateAsync<T>(stateName, cancellationToken);

        if (condRes.HasValue)
        {
            return condRes.Value;
        }

        throw new KeyNotFoundException(string.Format(CultureInfo.CurrentCulture, SR.ErrorNamedActorStateNotFound, stateName));
    }

    public async Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();
        var getCacheValue = stateChangeTracker.TryGet(stateName, out var state);
        if (getCacheValue.containsKey)
        {
            return getCacheValue.isMarkedAsRemoveOrExpired
                ? new ConditionalValue<T>(false, default)
                : new ConditionalValue<T>(true, (T)state!.Value);
        }
        
        var conditionalResult = await this.TryGetStateFromStateProviderAsync<T>(stateName, cancellationToken);
        if (conditionalResult.HasValue)
        {
            var stateMetadata = ActorStateCache.StateMetadata.Create(conditionalResult.Value.Value,
                StateChangeKind.None, conditionalResult.Value.TTLExpireTime);
            stateChangeTracker.Add(stateName, stateMetadata);
            return new ConditionalValue<T>(true, conditionalResult.Value.Value);
        }

        return new ConditionalValue<T>(false, default);
    }

    public async Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();
        var (cacheContainsKey, _) = stateChangeTracker.TryGet(stateName, out var state);
        if (cacheContainsKey && state is not null)
        {
            var updatedState = state with { Value = value, TTLExpireTime = null };
            if (state.ChangeKind is StateChangeKind.None or StateChangeKind.Remove)
            {
                updatedState = updatedState with { ChangeKind = StateChangeKind.Update };
            }

            stateChangeTracker.Set(stateName, updatedState);
        }
        else if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(),
                     stateName, cancellationToken))
        {
            stateChangeTracker.Add(stateName, ActorStateCache.StateMetadata.Create(value, StateChangeKind.Update));
        }
        else
        {
            stateChangeTracker.Set(stateName, ActorStateCache.StateMetadata.Create(value, StateChangeKind.Add));
        }
    }

    public async Task SetStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();
        var getCacheValue = stateChangeTracker.TryGet(stateName, out var state);
        if (getCacheValue.containsKey && state is not null)
        {
            var updatedState = state with { Value = state.Value, TTLExpireTime = DateTimeOffset.UtcNow.Add(ttl) };
            if (updatedState.ChangeKind is StateChangeKind.None or StateChangeKind.Remove)
            {
                updatedState = updatedState with { ChangeKind = StateChangeKind.Update };
            }
            stateChangeTracker.Set(stateName, updatedState);
        }
        else if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(),
                     stateName, cancellationToken))
        {
            stateChangeTracker.Add(stateName, ActorStateCache.StateMetadata.Create(value, StateChangeKind.Update, ttl));
        }
        else
        {
            stateChangeTracker.Set(stateName, ActorStateCache.StateMetadata.Create(value, StateChangeKind.Add, ttl));
        }
    }

    public async Task RemoveStateAsync(string stateName, CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();

        if (!(await this.TryRemoveStateAsync(stateName, cancellationToken)))
        {
            throw new KeyNotFoundException(string.Format(CultureInfo.CurrentCulture, SR.ErrorNamedActorStateNotFound, stateName));
        }
    }

    public async Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();

        var cacheGetResult = stateChangeTracker.TryGet(stateName, out var state);
        if (cacheGetResult.containsKey && state is not null)
        {
            if (cacheGetResult.isMarkedAsRemoveOrExpired)
            {
                stateChangeTracker.Remove(stateName);
                return false;
            }

            switch (state.ChangeKind)
            {
                case StateChangeKind.Remove:
                    return false;
                case StateChangeKind.Add:
                    stateChangeTracker.Remove(stateName);
                    return true;
            }

            var updatedState = state with { ChangeKind = StateChangeKind.Remove };
            stateChangeTracker.Set(stateName, updatedState);
            return true;
        }
        
        if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            stateChangeTracker.Add(stateName, ActorStateCache.StateMetadata.CreateForRemove());
            return true;
        }

        return false;
    }

    public async Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();
        var getCacheValue = stateChangeTracker.TryGet(stateName, out var state);
        if (getCacheValue.containsKey && state is not null)
        {
            //Check if the property was marked as remove in the cache
            return state.ChangeKind != StateChangeKind.Remove;
        }

        if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            return true;
        }

        return false;
    }

    public async Task<T> GetOrAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();

        var condRes = await this.TryGetStateAsync<T>(stateName, cancellationToken);

        if (condRes.HasValue)
        {
            return condRes.Value;
        }

        var changeKind = this.IsStateMarkedForRemove(stateName) ? StateChangeKind.Update : StateChangeKind.Add;

        var stateChangeTracker = GetContextualStateTracker();
        stateChangeTracker.Set(stateName, ActorStateCache.StateMetadata.Create(value, changeKind));
        return value;
    }

    public async Task<T> GetOrAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();

        var condRes = await this.TryGetStateAsync<T>(stateName, cancellationToken);

        if (condRes.HasValue)
        {
            return condRes.Value;
        }

        var changeKind = this.IsStateMarkedForRemove(stateName) ? StateChangeKind.Update : StateChangeKind.Add;

        var stateChangeTracker = GetContextualStateTracker();
        stateChangeTracker.Set(stateName, ActorStateCache.StateMetadata.Create(value, changeKind, ttl));
        return value;
    }

    public async Task<T> AddOrUpdateStateAsync<T>(
        string stateName,
        T addValue,
        Func<string, T, T> updateValueFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();
        var getCacheValue = stateChangeTracker.TryGet(stateName, out var state);
        if (getCacheValue.containsKey && state is not null)
        {
            //Check if the property was marked as remove in the cache
            if (state.ChangeKind == StateChangeKind.Remove)
            {
                stateChangeTracker.Set(stateName, ActorStateCache.StateMetadata.Create(addValue, StateChangeKind.Update));
                return addValue;
            }

            var newValue = updateValueFactory.Invoke(stateName, (T)state.Value);
            var updatedState = state with { Value = newValue };

            if (state.ChangeKind == StateChangeKind.None)
            {
                updatedState = updatedState with { ChangeKind = StateChangeKind.Update };
            }
            
            stateChangeTracker.Set(stateName, updatedState);
            return newValue;
        }

        var conditionalResult = await this.TryGetStateFromStateProviderAsync<T>(stateName, cancellationToken);
        if (conditionalResult.HasValue)
        {
            var newValue = updateValueFactory.Invoke(stateName, conditionalResult.Value.Value);
            stateChangeTracker.Add(stateName, ActorStateCache.StateMetadata.Create(newValue, StateChangeKind.Update));

            return newValue;
        }

        stateChangeTracker.Set(stateName, ActorStateCache.StateMetadata.Create(addValue, StateChangeKind.Add));
        return addValue;
    }

    public async Task<T> AddOrUpdateStateAsync<T>(
        string stateName,
        T addValue,
        Func<string, T, T> updateValueFactory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();
        var getCacheValue = stateChangeTracker.TryGet(stateName, out var state);
        if (getCacheValue.containsKey && state is not null)
        {
            if (state.ChangeKind == StateChangeKind.Remove)
            {
                stateChangeTracker.Set(stateName, ActorStateCache.StateMetadata.Create(addValue, StateChangeKind.Update, ttl));
                return addValue;
            }

            var newValue = updateValueFactory.Invoke(stateName, (T)state.Value);
            var updatedState = state with { Value = newValue };

            if (state.ChangeKind == StateChangeKind.None)
            {
                updatedState = updatedState with { ChangeKind = StateChangeKind.Update };
            }
            
            stateChangeTracker.Set(stateName, updatedState);

            return newValue;
        }

        var conditionalResult = await this.TryGetStateFromStateProviderAsync<T>(stateName, cancellationToken);
        if (conditionalResult.HasValue)
        {
            var newValue = updateValueFactory.Invoke(stateName, conditionalResult.Value.Value);
            stateChangeTracker.Add(stateName, ActorStateCache.StateMetadata.Create(newValue, StateChangeKind.Update, ttl));

            return newValue;
        }

        stateChangeTracker.Set(stateName, ActorStateCache.StateMetadata.Create(addValue, StateChangeKind.Add, ttl));
        return addValue;
    }

    public Task ClearCacheAsync(CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();

        var cache = GetContextualStateTracker();
        cache.Clear();
        
        return Task.CompletedTask;
    }

    public async Task SaveStateAsync(CancellationToken cancellationToken = default)
    {
        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();
        var (stateChanges, statesToRemove) = stateChangeTracker.BuildChangeList();

        if (stateChanges.Count > 0)
        {
            await this.actor.Host.StateProvider.SaveStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateChanges, cancellationToken);
        }
        
        //Remove the states from the tracker which were marked for removal
        if (statesToRemove.Count > 0)
        {
            foreach (var stateToRemove in statesToRemove)
            {
                stateChangeTracker.Remove(stateToRemove);
            }
        }
    }

    public Task SetStateContext(string stateContext)
    {
        context.Value = stateContext != null ? (stateContext, new ActorStateCache()) : (null, null);
        return Task.CompletedTask;
    }

    private bool IsStateMarkedForRemove(string stateName)
    {
        var stateChangeTracker = GetContextualStateTracker();

        var getCacheResult = stateChangeTracker.TryGet(stateName, out var state);
        return getCacheResult.containsKey && state is not null && state.ChangeKind == StateChangeKind.Remove;
    }

    private Task<ConditionalValue<ActorStateResponse<T>>> TryGetStateFromStateProviderAsync<T>(string stateName, CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();
        return this.actor.Host.StateProvider.TryLoadStateAsync<T>(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken);
    }

    private void EnsureStateProviderInitialized()
    {
        if (this.actor.Host.StateProvider == null)
        {
            throw new InvalidOperationException(
                "The actor was initialized without a state provider, and so cannot interact with state. " +
                "If this is inside a unit test, replace Actor.StateProvider with a mock.");
        }
    }

    private IActorStateCache GetContextualStateTracker() => context.Value.id != null ? context.Value.stateCache : defaultCache;
}
