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

internal interface IActorStateCache
{
    /// <summary>
    /// Adds the indicated value to the cache.
    /// </summary>
    /// <param name="stateName">The name of the state.</param>
    /// <param name="value">The cached value.</param>
    /// <param name="ttl">How far out the TTL expiry should be.</param>
    /// <typeparam name="T">The type of value getting cached.</typeparam>
    /// <returns><c>stateContainsKey</c> indicates if the cache already contains the key or not and
    /// <c>addedToState</c> indicates if the value was added or updated in the cache.</returns>
    (bool stateContainsKey, bool addedToState) Add<T>(string stateName, T value, TimeSpan? ttl = null);

    /// <summary>
    /// Adds the indicated value to the cache.
    /// </summary>
    /// <param name="stateName">The name of the state.</param>
    /// <param name="value">The cached value.</param>
    /// <param name="ttlExpiry">The TTL expiry timestamp.</param>
    /// <typeparam name="T">The type of value getting cached.</typeparam>
    /// <returns><c>stateContainsKey</c> indicates if the cache already contains the key or not and
    /// <c>addedToState</c> indicates if the value was added or updated in the cache.</returns>
    (bool stateContainsKey, bool addedToState) Add<T>(string stateName, T value, DateTimeOffset ttlExpiry);

    /// <summary>
    /// Sets the cache with the specified value whether it already exists or not.
    /// </summary>
    /// <param name="stateName">The name of the state to save the value to.</param>
    /// <param name="metadata">The state metadata to save to the cache.</param>
    void Set(string stateName, ActorStateCache.StateMetadata metadata);

    /// <summary>
    /// Removes the indicated state name from the cache.
    /// </summary>
    /// <param name="stateName">The name of the state to remove.</param>
    void Remove(string stateName);

    /// <summary>
    /// Retrieves the current state from the cache if available and not expired. 
    /// </summary>
    /// <param name="stateName">The name of the state to retrieve.</param>
    /// <param name="metadata">If available and not expired, the value of the state persisted in the cache.</param>
    /// <returns>True if the cache contains the state name; false if not.</returns>
    (bool containsKey, bool isMarkedAsRemoveOrExpired) TryGet(
        string stateName,
        out ActorStateCache.StateMetadata? metadata);

    /// <summary>
    /// Clears the all the data from the cache.
    /// </summary>
    void Clear();

    /// <summary>
    /// Builds out the change lists of states to update in the provider and states to remove from the cache. This
    /// is typically only called by invocation of the <c>SaveStateAsync</c> method in <see cref="ActorStateManager"/>.
    /// </summary>
    /// <returns></returns>
    (IReadOnlyList<ActorStateChange> stateChanges, IReadOnlyList<string> statesToRemove) BuildChangeList();

    /// <summary>
    /// Helper method that determines if a state metadata is expired.
    /// </summary>
    /// <param name="metadata">The metadata to evaluate.</param>
    /// <returns>True if the state metadata is marked for removal or the TTL has expired, otherwise false.</returns>
    bool IsMarkedAsRemoveOrExpired(ActorStateCache.StateMetadata metadata);
}
