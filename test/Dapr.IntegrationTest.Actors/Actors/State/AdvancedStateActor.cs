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
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.State;

/// <summary>
/// Implementation of <see cref="IAdvancedStateActor"/> that exercises the full
/// breadth of the Dapr state manager API in a way that validates in-memory caching
/// behaviour, concurrent key operations, and correct <c>GetOrAdd</c> / <c>AddOrUpdate</c>
/// semantics.
/// </summary>
public class AdvancedStateActor(ActorHost host) : Actor(host), IAdvancedStateActor
{
    /// <inheritdoc />
    public Task Ping() => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<string> SetAndGetWithinSameActivation(string key, string value)
    {
        // Write without saving — the value must be readable from cache immediately.
        await StateManager.SetStateAsync(key, value);
        return await StateManager.GetStateAsync<string>(key);
    }

    /// <inheritdoc />
    public Task<bool> ContainsKey(string key) =>
        StateManager.ContainsStateAsync(key);

    /// <inheritdoc />
    public async Task<StateCheckResult> RemoveAndCheckExists(string key)
    {
        await StateManager.TryRemoveStateAsync(key);
        var exists = await StateManager.ContainsStateAsync(key);
        return new StateCheckResult { Exists = exists };
    }

    /// <inheritdoc />
    public Task<string> GetOrAdd(string key, string defaultValue) =>
        StateManager.GetOrAddStateAsync(key, defaultValue);

    /// <inheritdoc />
    public Task<string> AddOrUpdate(string key, string addValue, string updateValue) =>
        StateManager.AddOrUpdateStateAsync(key, addValue, (_, _) => updateValue);

    /// <inheritdoc />
    public async Task<bool> TryAdd(string key, string value) =>
        await StateManager.TryAddStateAsync(key, value);

    /// <inheritdoc />
    public async Task<StateCheckResult> TryGet(string key)
    {
        var result = await StateManager.TryGetStateAsync<string>(key);
        return new StateCheckResult
        {
            Exists = result.HasValue,
            Value = result.HasValue ? result.Value : null,
        };
    }

    /// <inheritdoc />
    public async Task<string[]> SetMultipleAndGetAll(
        string key1, string value1,
        string key2, string value2)
    {
        await StateManager.SetStateAsync(key1, value1);
        await StateManager.SetStateAsync(key2, value2);
        return
        [
            await StateManager.GetStateAsync<string>(key1),
            await StateManager.GetStateAsync<string>(key2),
        ];
    }

    /// <inheritdoc />
    public async Task<string> OverwriteAndRead(string key, string value1, string value2)
    {
        await StateManager.SetStateAsync(key, value1);
        await StateManager.SetStateAsync(key, value2);
        return await StateManager.GetStateAsync<string>(key);
    }

    /// <inheritdoc />
    public async Task<string?> Read(string key)
    {
        var result = await StateManager.TryGetStateAsync<string>(key);
        return result.HasValue ? result.Value : null;
    }
}
