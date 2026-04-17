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

namespace Dapr.IntegrationTest.Actors.Generators.Actors;

/// <summary>
/// State record used by the remote actor for get/set operations.
/// </summary>
public record RemoteState(string Value);

/// <summary>
/// Actor interface for the server-side actor that manages state.
/// Extends <see cref="IPingActor"/> so the runtime readiness check can be performed.
/// </summary>
public interface IRemoteActor : IPingActor
{
    /// <summary>
    /// Returns the current state of the actor.
    /// </summary>
    Task<RemoteState> GetState();

    /// <summary>
    /// Sets the state of the actor.
    /// </summary>
    /// <param name="state">The new state to set.</param>
    Task SetState(RemoteState state);

    /// <summary>
    /// Returns a greeting message for the specified name.
    /// </summary>
    /// <param name="name">The name to greet.</param>
    Task<string> SayHello(string name);

    /// <summary>
    /// A fire-and-forget method with no parameters and no return value.
    /// </summary>
    Task IncrementCallCount();

    /// <summary>
    /// Returns the current call count.
    /// </summary>
    Task<int> GetCallCount();
}
