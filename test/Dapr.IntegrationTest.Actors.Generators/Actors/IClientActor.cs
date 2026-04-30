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

using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Generators;

namespace Dapr.IntegrationTest.Actors.Generators.Actors;

/// <summary>
/// State record used by the client-side generated actor client.
/// </summary>
public record ClientState(string Value);

/// <summary>
/// Actor interface decorated with <see cref="GenerateActorClientAttribute"/> to trigger
/// source generation of a strongly-typed client (<c>ClientActorClient</c>).
/// The methods use <see cref="ActorMethodAttribute"/> to map to the actual server-side
/// actor method names.
/// </summary>
[GenerateActorClient]
public interface IClientActor
{
    /// <summary>
    /// Gets the current state from the remote actor.
    /// </summary>
    [ActorMethod(Name = "GetState")]
    Task<ClientState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the state on the remote actor.
    /// </summary>
    [ActorMethod(Name = "SetState")]
    Task SetStateAsync(ClientState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the SayHello method on the remote actor.
    /// </summary>
    [ActorMethod(Name = "SayHello")]
    Task<string> SayHelloAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the IncrementCallCount method on the remote actor.
    /// </summary>
    [ActorMethod(Name = "IncrementCallCount")]
    Task IncrementCallCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the call count from the remote actor.
    /// </summary>
    [ActorMethod(Name = "GetCallCount")]
    Task<int> GetCallCountAsync(CancellationToken cancellationToken = default);
}
