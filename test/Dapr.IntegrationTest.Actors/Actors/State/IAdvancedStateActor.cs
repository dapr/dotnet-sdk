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

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.State;

/// <summary>
/// Result returned by state existence and try-get operations.
/// </summary>
public sealed class StateCheckResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the state key exists in the store.
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// Gets or sets the stored value, or <see langword="null"/> when the key does not exist.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Actor interface that exercises the full breadth of state management operations,
/// including <c>AddState</c>, <c>GetOrAdd</c>, <c>AddOrUpdate</c>, <c>ContainsState</c>,
/// <c>TryGetState</c>, <c>RemoveState</c>, and <c>SaveState</c> — both individually and in
/// combinations that validate the in-memory caching behaviour of the state manager.
/// </summary>
public interface IAdvancedStateActor : IPingActor, IActor
{
    /// <summary>
    /// Sets <paramref name="key"/> to <paramref name="value"/> without calling
    /// <see cref="IActorStateManager.SaveStateAsync"/>, then immediately returns the cached value.
    /// This verifies that the in-memory write-through cache makes a just-set value readable
    /// within the same activation without a round-trip to the state store.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="value">The value to write.</param>
    Task<string> SetAndGetWithinSameActivation(string key, string value);

    /// <summary>
    /// Returns whether <paramref name="key"/> currently exists in state.
    /// </summary>
    /// <param name="key">The state key to check.</param>
    Task<bool> ContainsKey(string key);

    /// <summary>
    /// Removes <paramref name="key"/> from state, then immediately checks whether it still
    /// exists, validating that a removal is reflected in the cache before it is persisted.
    /// </summary>
    /// <param name="key">The state key to remove.</param>
    Task<StateCheckResult> RemoveAndCheckExists(string key);

    /// <summary>
    /// Calls <c>GetOrAdd</c> with the supplied default.  If the key already exists the stored
    /// value is returned; otherwise the default is stored and returned.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="defaultValue">The default value to use when the key does not exist.</param>
    Task<string> GetOrAdd(string key, string defaultValue);

    /// <summary>
    /// Calls <c>AddOrUpdate</c>: adds the key with <paramref name="addValue"/> when absent,
    /// or replaces the existing value with <paramref name="updateValue"/> when present.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="addValue">The value to write when the key does not yet exist.</param>
    /// <param name="updateValue">The value to write when the key already exists.</param>
    Task<string> AddOrUpdate(string key, string addValue, string updateValue);

    /// <summary>
    /// Attempts to add <paramref name="value"/> via <c>AddStateAsync</c> when the key does
    /// not already exist; returns <see langword="true"/> when the add succeeds, or
    /// <see langword="false"/> when the key was already present.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="value">The value to write if the key is absent.</param>
    Task<bool> TryAdd(string key, string value);

    /// <summary>
    /// Tries to retrieve <paramref name="key"/> and returns a <see cref="StateCheckResult"/>
    /// describing whether the key exists and, if so, its value.
    /// </summary>
    /// <param name="key">The state key.</param>
    Task<StateCheckResult> TryGet(string key);

    /// <summary>
    /// Sets multiple independent keys in a single actor activation and returns all their values,
    /// verifying that independent keys do not interfere with each other in the cache.
    /// </summary>
    /// <param name="key1">First state key.</param>
    /// <param name="value1">First value.</param>
    /// <param name="key2">Second state key.</param>
    /// <param name="value2">Second value.</param>
    Task<string[]> SetMultipleAndGetAll(string key1, string value1, string key2, string value2);

    /// <summary>
    /// Sets <paramref name="key"/> to <paramref name="value1"/>, then overwrites it with
    /// <paramref name="value2"/> in the same activation, and returns the final value.
    /// Verifies that a second <c>SetStateAsync</c> correctly replaces the first cached value.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="value1">The initial value.</param>
    /// <param name="value2">The overwrite value.</param>
    Task<string> OverwriteAndRead(string key, string value1, string value2);

    /// <summary>
    /// Returns the current value stored under <paramref name="key"/>, or <see langword="null"/>
    /// when the key does not exist.
    /// </summary>
    /// <param name="key">The state key to read.</param>
    Task<string?> Read(string key);
}
