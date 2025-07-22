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
using Dapr.E2E.Test.Actors.ErrorTesting;

namespace Dapr.E2E.Test.App.ErrorTesting;

public class Regression762Actor : Actor, IRegression762Actor
{
    public Regression762Actor(ActorHost host) : base(host)
    {
    }

    public Task Ping()
    {
        return Task.CompletedTask;
    }

    public async Task<string> GetState(string id)
    {
        var data = await this.StateManager.TryGetStateAsync<string>(id);

        if (data.HasValue)
        {
            return data.Value;
        }
        return string.Empty;
    }        

    public async Task RemoveState(string id)
    {
        await this.StateManager.TryRemoveStateAsync(id);
    }

    public async Task SaveState(StateCall call)
    {
        if (call.Operation == "ThrowException")
        {
            await this.StateManager.SetStateAsync<string>(call.Key, call.Value);
            throw new NotImplementedException();
        }
        else if (call.Operation == "SetState")
        {
            await this.StateManager.SetStateAsync<string>(call.Key, call.Value);
        }
        else if (call.Operation == "SaveState")
        {
            await this.StateManager.SaveStateAsync();
        }
    }
}