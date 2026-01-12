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

using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Reentrancy;

public class ReentrantActor : Actor, IReentrantActor
{
    public ReentrantActor(ActorHost host)
        : base(host)
    {
    }

    public Task Ping()
    {
        return Task.CompletedTask;
    }

    // An actor method that exercises reentrancy by calling more methods in the same actor.
    // Can be configured to different reentrant depths via the ReentrantCallOptions but will
    // always make at least one additional call.
    public async Task ReentrantCall(ReentrantCallOptions callOptions)
    {
        await UpdateState(true, callOptions.CallNumber);
        var actor = this.ProxyFactory.CreateActorProxy<IReentrantActor>(this.Id, "ReentrantActor");
        if (callOptions == null || callOptions.CallsRemaining <= 1)
        {   
            await actor.Ping();
        }
        else
        {
            await actor.ReentrantCall(new ReentrantCallOptions 
            { 
                CallsRemaining = callOptions.CallsRemaining - 1,
                CallNumber = callOptions.CallNumber + 1,
            });
        }
        await UpdateState(false, callOptions.CallNumber);
    }

    public Task<State> GetState(int callNumber) 
    {
        return this.StateManager.GetOrAddStateAsync<State>($"reentrant-record{callNumber}", new State());
    }

    private async Task UpdateState(bool isEnter, int callNumber)
    {
        var state = await this.StateManager.GetOrAddStateAsync<State>($"reentrant-record{callNumber}", new State());
        state.Records.Add(new CallRecord { IsEnter = isEnter, Timestamp = System.DateTime.Now, CallNumber = callNumber });
        await this.StateManager.SetStateAsync<State>($"reentrant-record{callNumber}", state);

        if (!isEnter)
        {
            await this.StateManager.SaveStateAsync();
        }
    }
}