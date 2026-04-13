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

using System;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.State;

/// <summary>
/// Implementation of <see cref="IStateActor"/> that stores and retrieves string values
/// from the Dapr state store, with optional TTL support.
/// </summary>
public class StateActor(ActorHost host) : Actor(host), IStateActor
{
    /// <inheritdoc />
    public Task Ping() => Task.CompletedTask;

    /// <inheritdoc />
    public Task<string> GetState(string key) =>
        StateManager.GetStateAsync<string>(key);

    /// <inheritdoc />
    public Task SetState(string key, string value, TimeSpan? ttl) =>
        ttl.HasValue
            ? StateManager.SetStateAsync<string>(key, value, ttl: ttl.Value)
            : StateManager.SetStateAsync<string>(key, value);
}
