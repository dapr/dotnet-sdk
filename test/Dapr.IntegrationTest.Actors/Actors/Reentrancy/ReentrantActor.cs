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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.Reentrancy;

/// <summary>
/// Implementation of <see cref="IReentrantActor"/> that recursively calls itself
/// to produce a chain of reentrant invocations.
/// </summary>
public class ReentrantActor(ActorHost host) : Actor(host), IReentrantActor
{
    /// <inheritdoc />
    public Task Ping(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task ReentrantCall(ReentrantCallOptions callOptions)
    {
        await UpdateState(isEnter: true, callOptions.CallNumber);

        var self = ProxyFactory.CreateActorProxy<IReentrantActor>(Id, "ReentrantActor");
        if (callOptions.CallsRemaining <= 1)
        {
            await self.Ping();
        }
        else
        {
            await self.ReentrantCall(new ReentrantCallOptions
            {
                CallsRemaining = callOptions.CallsRemaining - 1,
                CallNumber = callOptions.CallNumber + 1,
            });
        }

        await UpdateState(isEnter: false, callOptions.CallNumber);
    }

    /// <inheritdoc />
    public Task<ReentrantCallState> GetState(int callNumber) =>
        StateManager.GetOrAddStateAsync($"reentrant-record{callNumber}", new ReentrantCallState());

    private async Task UpdateState(bool isEnter, int callNumber)
    {
        var stateKey = $"reentrant-record{callNumber}";
        var state = await StateManager.GetOrAddStateAsync(stateKey, new ReentrantCallState());
        state.Records.Add(new CallRecord
        {
            IsEnter = isEnter,
            Timestamp = DateTime.Now,
            CallNumber = callNumber,
        });
        await StateManager.SetStateAsync(stateKey, state);

        if (!isEnter)
        {
            await StateManager.SaveStateAsync();
        }
    }
}
