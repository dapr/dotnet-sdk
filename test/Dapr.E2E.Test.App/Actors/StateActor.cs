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
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.State;

public class StateActor : Actor, IStateActor
{
    public StateActor(ActorHost host)
        : base(host)
    {
    }

    public Task Ping()
    {
        return Task.CompletedTask;
    }

    public Task<string> GetState(string key)
    {
        return this.StateManager.GetStateAsync<string>(key);
    }

    public Task SetState(string key, string value, TimeSpan? ttl)
    {
        if (ttl.HasValue)
        {
            return this.StateManager.SetStateAsync<String>(key, value, ttl: ttl.Value);
        }
        return this.StateManager.SetStateAsync<String>(key, value);
    }
}