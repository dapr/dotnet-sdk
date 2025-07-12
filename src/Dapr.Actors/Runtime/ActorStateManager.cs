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
    private readonly Dictionary<string, StateMetadata> defaultTracker;
    private static AsyncLocal<(string id, Dictionary<string, StateMetadata> tracker)> context = new AsyncLocal<(string, Dictionary<string, StateMetadata>)>();

    internal ActorStateManager(Actor actor)
    {
        this.actor = actor;
        this.actorTypeName = actor.Host.ActorTypeInfo.ActorTypeName;
        this.defaultTracker = new Dictionary<string, StateMetadata>();
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

        var stateChangeTracker = GetContextualStateTracker();

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];

            // Check if the property was marked as remove or is expired in the cache
            if (stateMetadata.ChangeKind == StateChangeKind.Remove || (stateMetadata.TTLExpireTime.HasValue && stateMetadata.TTLExpireTime.Value <= DateTimeOffset.UtcNow))
            {
                stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Update);
                return true;
            }

            return false;
        }

        if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            return false;
        }

        stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Add);
        return true;
    }

    public async Task<bool> TryAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];

            // Check if the property was marked as remove in the cache or has been expired.
            if (stateMetadata.ChangeKind == StateChangeKind.Remove || (stateMetadata.TTLExpireTime.HasValue && stateMetadata.TTLExpireTime.Value <= DateTimeOffset.UtcNow))
            {
                stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Update, ttl: ttl);
                return true;
            }

            return false;
        }

        if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            return false;
        }

        stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Add, ttl: ttl);
        return true;
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

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];

            // Check if the property was marked as remove in the cache or is expired
            if (stateMetadata.ChangeKind == StateChangeKind.Remove || (stateMetadata.TTLExpireTime.HasValue && stateMetadata.TTLExpireTime.Value <= DateTimeOffset.UtcNow))
            {
                return new ConditionalValue<T>(false, default);
            }

            return new ConditionalValue<T>(true, (T)stateMetadata.Value);
        }

        var conditionalResult = await this.TryGetStateFromStateProviderAsync<T>(stateName, cancellationToken);
        if (conditionalResult.HasValue)
        {
            stateChangeTracker.Add(stateName, StateMetadata.Create(conditionalResult.Value.Value, StateChangeKind.None, ttlExpireTime: conditionalResult.Value.TTLExpireTime));
            return new ConditionalValue<T>(true, conditionalResult.Value.Value);
        }

        return new ConditionalValue<T>(false, default);
    }

    public async Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];
            stateMetadata.Value = value;
            stateMetadata.TTLExpireTime = null;

            if (stateMetadata.ChangeKind == StateChangeKind.None ||
                stateMetadata.ChangeKind == StateChangeKind.Remove)
            {
                stateMetadata.ChangeKind = StateChangeKind.Update;
            }
        }
        else if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            stateChangeTracker.Add(stateName, StateMetadata.Create(value, StateChangeKind.Update));
        }
        else
        {
            stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Add);
        }
    }

    public async Task SetStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];
            stateMetadata.Value = value;
            stateMetadata.TTLExpireTime = DateTimeOffset.UtcNow.Add(ttl);

            if (stateMetadata.ChangeKind == StateChangeKind.None ||
                stateMetadata.ChangeKind == StateChangeKind.Remove)
            {
                stateMetadata.ChangeKind = StateChangeKind.Update;
            }
        }
        else if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            stateChangeTracker.Add(stateName, StateMetadata.Create(value, StateChangeKind.Update, ttl: ttl));
        }
        else
        {
            stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Add, ttl: ttl);
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

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];

            if (stateMetadata.TTLExpireTime.HasValue && stateMetadata.TTLExpireTime.Value <= DateTimeOffset.UtcNow)
            {
                stateChangeTracker.Remove(stateName);
                return false;
            }

            switch (stateMetadata.ChangeKind)
            {
                case StateChangeKind.Remove:
                    return false;
                case StateChangeKind.Add:
                    stateChangeTracker.Remove(stateName);
                    return true;
            }

            stateMetadata.ChangeKind = StateChangeKind.Remove;
            return true;
        }

        if (await this.actor.Host.StateProvider.ContainsStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateName, cancellationToken))
        {
            stateChangeTracker.Add(stateName, StateMetadata.CreateForRemove());
            return true;
        }

        return false;
    }

    public async Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken)
    {
        ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];

            // Check if the property was marked as remove in the cache
            return stateMetadata.ChangeKind != StateChangeKind.Remove;
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
        stateChangeTracker[stateName] = StateMetadata.Create(value, changeKind);
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
        stateChangeTracker[stateName] = StateMetadata.Create(value, changeKind, ttl: ttl);
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

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];

            // Check if the property was marked as remove in the cache
            if (stateMetadata.ChangeKind == StateChangeKind.Remove)
            {
                stateChangeTracker[stateName] = StateMetadata.Create(addValue, StateChangeKind.Update);
                return addValue;
            }

            var newValue = updateValueFactory.Invoke(stateName, (T)stateMetadata.Value);
            stateMetadata.Value = newValue;

            if (stateMetadata.ChangeKind == StateChangeKind.None)
            {
                stateMetadata.ChangeKind = StateChangeKind.Update;
            }

            return newValue;
        }

        var conditionalResult = await this.TryGetStateFromStateProviderAsync<T>(stateName, cancellationToken);
        if (conditionalResult.HasValue)
        {
            var newValue = updateValueFactory.Invoke(stateName, conditionalResult.Value.Value);
            stateChangeTracker.Add(stateName, StateMetadata.Create(newValue, StateChangeKind.Update));

            return newValue;
        }

        stateChangeTracker[stateName] = StateMetadata.Create(addValue, StateChangeKind.Add);
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

        if (stateChangeTracker.ContainsKey(stateName))
        {
            var stateMetadata = stateChangeTracker[stateName];

            // Check if the property was marked as remove in the cache
            if (stateMetadata.ChangeKind == StateChangeKind.Remove)
            {
                stateChangeTracker[stateName] = StateMetadata.Create(addValue, StateChangeKind.Update, ttl: ttl);
                return addValue;
            }

            var newValue = updateValueFactory.Invoke(stateName, (T)stateMetadata.Value);
            stateMetadata.Value = newValue;

            if (stateMetadata.ChangeKind == StateChangeKind.None)
            {
                stateMetadata.ChangeKind = StateChangeKind.Update;
            }

            return newValue;
        }

        var conditionalResult = await this.TryGetStateFromStateProviderAsync<T>(stateName, cancellationToken);
        if (conditionalResult.HasValue)
        {
            var newValue = updateValueFactory.Invoke(stateName, conditionalResult.Value.Value);
            stateChangeTracker.Add(stateName, StateMetadata.Create(newValue, StateChangeKind.Update, ttl: ttl));

            return newValue;
        }

        stateChangeTracker[stateName] = StateMetadata.Create(addValue, StateChangeKind.Add, ttl: ttl);
        return addValue;
    }

    public Task ClearCacheAsync(CancellationToken cancellationToken)
    {
        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();

        stateChangeTracker.Clear();
        return Task.CompletedTask;
    }

    public async Task SaveStateAsync(CancellationToken cancellationToken = default)
    {
        EnsureStateProviderInitialized();

        var stateChangeTracker = GetContextualStateTracker();

        if (stateChangeTracker.Count > 0)
        {
            var stateChangeList = new List<ActorStateChange>();
            var statesToRemove = new List<string>();

            foreach (var stateName in stateChangeTracker.Keys)
            {
                var stateMetadata = stateChangeTracker[stateName];

                if (stateMetadata.ChangeKind != StateChangeKind.None)
                {
                    stateChangeList.Add(
                        new ActorStateChange(stateName, stateMetadata.Type, stateMetadata.Value, stateMetadata.ChangeKind, stateMetadata.TTLExpireTime));

                    if (stateMetadata.ChangeKind == StateChangeKind.Remove)
                    {
                        statesToRemove.Add(stateName);
                    }

                    // Mark the states as unmodified so that tracking for next invocation is done correctly.
                    stateMetadata.ChangeKind = StateChangeKind.None;
                }
            }

            if (stateChangeList.Count > 0)
            {
                await this.actor.Host.StateProvider.SaveStateAsync(this.actorTypeName, this.actor.Id.ToString(), stateChangeList.AsReadOnly(), cancellationToken);
            }

            // Remove the states from tracker whcih were marked for removal.
            foreach (var stateToRemove in statesToRemove)
            {
                stateChangeTracker.Remove(stateToRemove);
            }
        }
    }

    public Task SetStateContext(string stateContext)
    {
        if (stateContext != null)
        {
            context.Value = (stateContext, new Dictionary<string, StateMetadata>());
        }
        else
        {
            context.Value = (null, null);
        }

        return Task.CompletedTask;
    }

    private bool IsStateMarkedForRemove(string stateName)
    {
        var stateChangeTracker = GetContextualStateTracker();

        if (stateChangeTracker.ContainsKey(stateName) &&
            stateChangeTracker[stateName].ChangeKind == StateChangeKind.Remove)
        {
            return true;
        }

        return false;
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

    private Dictionary<string, StateMetadata> GetContextualStateTracker()
    {
        if (context.Value.id != null)
        {
            return context.Value.tracker;
        }
        else
        {
            return defaultTracker;
        }
    }

    private sealed class StateMetadata
    {
        private StateMetadata(object value, Type type, StateChangeKind changeKind, DateTimeOffset? ttlExpireTime = null, TimeSpan? ttl = null)
        {
            this.Value = value;
            this.Type = type;
            this.ChangeKind = changeKind;

            if (ttlExpireTime.HasValue && ttl.HasValue) {
                throw new ArgumentException("Cannot specify both TTLExpireTime and TTL");
            }
            if (ttl.HasValue) {
                this.TTLExpireTime = DateTimeOffset.UtcNow.Add(ttl.Value);
            } else {
                this.TTLExpireTime = ttlExpireTime;
            }
        }

        public object Value { get; set; }

        public StateChangeKind ChangeKind { get; set; }

        public Type Type { get; }

        public DateTimeOffset? TTLExpireTime { get; set; }

        public static StateMetadata Create<T>(T value, StateChangeKind changeKind)
        {
            return new StateMetadata(value, typeof(T), changeKind);
        }

        public static StateMetadata Create<T>(T value, StateChangeKind changeKind, DateTimeOffset? ttlExpireTime)
        {
            return new StateMetadata(value, typeof(T), changeKind, ttlExpireTime: ttlExpireTime);
        }

        public static StateMetadata Create<T>(T value, StateChangeKind changeKind, TimeSpan? ttl)
        {
            return new StateMetadata(value, typeof(T), changeKind, ttl: ttl);
        }

        public static StateMetadata CreateForRemove()
        {
            return new StateMetadata(null, typeof(object), StateChangeKind.Remove);
        }
    }
}