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

using System;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.Regression;

/// <summary>
/// Implementation of <see cref="IRegressionActor"/> that reproduces the scenario from
/// GitHub issue #762: an exception thrown mid-method must roll back pending state changes.
/// </summary>
public class RegressionActor : Actor, IRegressionActor
{
    /// <summary>
    /// Initializes a new instance of <see cref="RegressionActor"/>.
    /// </summary>
    /// <param name="host">The actor host provided by the Dapr runtime.</param>
    public RegressionActor(ActorHost host) : base(host)
    {
    }

    /// <inheritdoc />
    public Task Ping() => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<string> GetState(string id)
    {
        var data = await StateManager.TryGetStateAsync<string>(id);
        return data.HasValue ? data.Value : string.Empty;
    }

    /// <inheritdoc />
    public async Task RemoveState(string id)
    {
        await StateManager.TryRemoveStateAsync(id);
    }

    /// <inheritdoc />
    public async Task SaveState(StateCall call)
    {
        if (call.Operation == "ThrowException")
        {
            await StateManager.SetStateAsync<string>(call.Key!, call.Value!);
            throw new NotImplementedException("Intentional exception to test state rollback.");
        }
        else if (call.Operation == "SetState")
        {
            await StateManager.SetStateAsync<string>(call.Key!, call.Value!);
        }
        else if (call.Operation == "SaveState")
        {
            await StateManager.SaveStateAsync();
        }
    }
}
