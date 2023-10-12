// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using Dapr.Actors.Runtime;

namespace GeneratedActor;

internal sealed class RemoteActor : Actor, IRemoteActor
{
    private readonly ILogger<RemoteActor> logger;

    private RemoteState currentState = new("default");

    public RemoteActor(ActorHost host, ILogger<RemoteActor> logger)
        : base(host)
    {
        this.logger = logger;
    }

    public Task<RemoteState> GetState()
    {
        this.logger.LogInformation("GetStateAsync called.");

        return Task.FromResult(this.currentState);
    }

    public Task SetState(RemoteState state)
    {
        this.logger.LogInformation("SetStateAsync called.");

        this.currentState = state;

        return Task.CompletedTask;
    }
}