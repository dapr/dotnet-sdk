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

namespace Dapr.Actors.Communication.Client;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

internal class ActorNonRemotingClient
{
    private readonly IDaprInteractor daprInteractor;

    public ActorNonRemotingClient(IDaprInteractor daprInteractor)
    {
        this.daprInteractor = daprInteractor;
    }

    /// <summary>
    /// Invokes an Actor method on Dapr runtime without remoting.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="methodName">Method name to invoke.</param>
    /// <param name="jsonPayload">Serialized body.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<Stream> InvokeActorMethodWithoutRemotingAsync(string actorType, string actorId, string methodName, string jsonPayload, CancellationToken cancellationToken = default)
    {
        return this.daprInteractor.InvokeActorMethodWithoutRemotingAsync(actorType, actorId, methodName, jsonPayload, cancellationToken);
    }
}