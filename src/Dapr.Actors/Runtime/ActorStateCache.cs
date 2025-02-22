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

#nullable enable
using System;
using System.Collections.Generic;

namespace Dapr.Actors.Runtime;

internal sealed class ActorStateCache : IActorStateCache
{
    /// <summary>
    /// Maintains the cache state.
    /// </summary>
    private readonly Dictionary<string, StateMetadata> stateMetadata = new();
    
    /// <summary>
    /// Adds the indicated value to the cache.
    /// </summary>
    /// <param name="stateName">The name of the state.</param>
    /// <param name="value">The cached value.</param>
    /// <param name="ttl">How far out the TTL expiry should be.</param>
    /// <typeparam name="T">The type of value getting cached.</typeparam>
    /// <returns><c>stateContainsKey</c> indicates if the cache already contains the key or not and
    /// <c>addedToState</c> indicates if the value was added or updated in the cache.</returns>
    public (bool stateContainsKey, bool addedToState) Add<T>(string stateName, T value, TimeSpan? ttl = null)
    {
        if (!stateMetadata.TryGetValue(stateName, out var state))
        {
            stateMetadata.Add(stateName, StateMetadata.Create(value, StateChangeKind.Add, ttl));
            return (false, true);
        }

        if (!IsMarkedAsRemoveOrExpired(state))
        {
            return (true, false);
        }

        stateMetadata[stateName] = StateMetadata.Create(value, StateChangeKind.Update, ttl);
        return (true, true);
    }

    /// <summary>
    /// Adds the indicated value to the cache.
    /// </summary>
    /// <param name="stateName">The name of the state.</param>
    /// <param name="value">The cached value.</param>
    /// <param name="ttlExpiry">The TTL expiry timestamp.</param>
    /// <typeparam name="T">The type of value getting cached.</typeparam>
    /// <returns><c>stateContainsKey</c> indicates if the cache already contains the key or not and
    /// <c>addedToState</c> indicates if the value was added or updated in the cache.</returns>
    public (bool stateContainsKey, bool addedToState) Add<T>(string stateName, T value, DateTimeOffset ttlExpiry)
    {
        if (!stateMetadata.TryGetValue(stateName, out var state))
        {
            stateMetadata.Add(stateName, StateMetadata.Create(value, StateChangeKind.Add, ttlExpiry));
            return (false, true);
        }

        if (!IsMarkedAsRemoveOrExpired(state))
        {
            return (true, false);
        }

        stateMetadata[stateName] = StateMetadata.Create(value, StateChangeKind.Update, ttlExpiry);
        return (true, true);

    }

    /// <summary>
    /// Sets the cache with the specified value whether it already exists or not.
    /// </summary>
    /// <param name="stateName">The name of the state to save the value to.</param>
    /// <param name="metadata">The state metadata to save to the cache.</param>
    public void Set(string stateName, StateMetadata metadata)
    {
        stateMetadata[stateName] = metadata;
    }

    /// <summary>
    /// Removes the indicated state name from the cache.
    /// </summary>
    /// <param name="stateName">The name of the state to remove.</param>
    public void Remove(string stateName) => stateMetadata.Remove(stateName);

    /// <summary>
    /// Retrieves the current state from the cache if available and not expired. 
    /// </summary>
    /// <param name="stateName">The name of the state to retrieve.</param>
    /// <param name="metadata">If available and not expired, the value of the state persisted in the cache.</param>
    /// <returns>True if the cache contains the state name; false if not.</returns>
    public (bool containsKey, bool isMarkedAsRemoveOrExpired) TryGet(string stateName, out StateMetadata? metadata)
    {
        var isMarkedAsRemoveOrExpired = false;
        metadata = null;

        if (!stateMetadata.TryGetValue(stateName, out var state))
        {
            return (false, false);
        }

        if (IsMarkedAsRemoveOrExpired(state))
        {
            isMarkedAsRemoveOrExpired = true;
        }
            
        metadata = state;
        return (true, isMarkedAsRemoveOrExpired);

    }

    /// <summary>
    /// Clears the all the data from the cache.
    /// </summary>
    public void Clear()
    {
        stateMetadata.Clear();
    }

    /// <summary>
    /// Builds out the change lists of states to update in the provider and states to remove from the cache. This
    /// is typically only called by invocation of the <c>SaveStateAsync</c> method in <see cref="ActorStateManager"/>.
    /// </summary>
    /// <returns>The list of state changes and states to remove from the cache.</returns>
    public (IReadOnlyList<ActorStateChange> stateChanges, IReadOnlyList<string> statesToRemove) BuildChangeList()
    {
        var stateChanges = new List<ActorStateChange>();
        var statesToRemove = new List<string>();

        if (stateMetadata.Count == 0)
        {
            return (stateChanges, statesToRemove);
        }

        foreach (var stateName in stateMetadata.Keys)
        {
            var metadata = stateMetadata[stateName];
            if (metadata.ChangeKind is not StateChangeKind.None)
            {
                stateChanges.Add(new ActorStateChange(stateName, metadata.Type, metadata.Value, metadata.ChangeKind, metadata.TTLExpireTime));

                if (metadata.ChangeKind is StateChangeKind.Remove)
                {
                    statesToRemove.Add(stateName);
                }
                
                //Mark the states as unmodified so the tracking for the next invocation is done correctly
                var updatedState = metadata with { ChangeKind = StateChangeKind.None };
                stateMetadata[stateName] = updatedState;
            }
        }

        return (stateChanges, statesToRemove);
    }

    /// <summary>
    /// Helper method that determines if a state metadata is expired.
    /// </summary>
    /// <param name="metadata">The metadata to evaluate.</param>
    /// <returns>True if the state metadata is marked for removal or the TTL has expired, otherwise false.</returns>
    public bool IsMarkedAsRemoveOrExpired(StateMetadata metadata) =>
        metadata.ChangeKind == StateChangeKind.Remove || (metadata.TTLExpireTime.HasValue &&
                                                          metadata.TTLExpireTime.Value <= DateTimeOffset.UtcNow);
    
    /// <summary>
    /// Exposed for testing only.
    /// </summary>
    /// <returns></returns>
    internal Dictionary<string, StateMetadata> GetStateMetadata() => stateMetadata;
     
    internal sealed record StateMetadata
    {
        /// <summary>
        /// This should only be used for testing purposes. Use the static `Create` methods for any actual usage.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <param name="changeKind"></param>
        /// <param name="ttlExpireTime"></param>
        /// <param name="ttl"></param>
        /// <exception cref="ArgumentException"></exception>
        internal StateMetadata(object? value, Type type, StateChangeKind changeKind, DateTimeOffset? ttlExpireTime = null, TimeSpan? ttl = null)
        {
            this.Value = value;
            this.Type = type;
            this.ChangeKind = changeKind;

            if (ttlExpireTime.HasValue && ttl.HasValue) {
                throw new ArgumentException("Cannot specify both TTLExpireTime and TTL");
            }

            this.TTLExpireTime = ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : ttlExpireTime;
        }

        public object? Value { get; init; }

        public StateChangeKind ChangeKind { get; init; }

        public Type Type { get; init; }

        public DateTimeOffset? TTLExpireTime { get; init; }

        public static StateMetadata Create<T>(T? value, StateChangeKind changeKind) =>
            new(value, typeof(T), changeKind);

        public static StateMetadata Create<T>(T? value, StateChangeKind changeKind, DateTimeOffset? ttlExpireTime) =>
            new(value, typeof(T), changeKind, ttlExpireTime: ttlExpireTime);

        public static StateMetadata Create<T>(T? value, StateChangeKind changeKind, TimeSpan? ttl) =>
            new(value, typeof(T), changeKind, ttl: ttl);

        public static StateMetadata CreateForRemove() => new(null, typeof(object), StateChangeKind.Remove);
    }
}
