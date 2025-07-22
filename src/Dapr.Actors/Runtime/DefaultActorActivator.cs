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

namespace Dapr.Actors.Runtime;

/// <summary>
/// A default implementation of <see cref="ActorActivator" /> that uses <see cref="Activator.CreateInstance(Type)" />.
/// </summary>
public class DefaultActorActivator : ActorActivator
{
    /// <summary>
    /// Creates the actor instance and returns it inside an instance of <see cref="ActorActivatorState" />.
    /// </summary>
    /// <param name="host">The actor host specifying information needed for the creation of the actor.</param>
    /// <returns>
    /// Asynchronously returns n instance of <see cref="ActorActivatorState" />. The <see cref="ActorActivatorState" /> 
    /// instance will be provided to <see cref="DeleteAsync" /> when the actor is ready for deletion.
    /// </returns>
    /// <remarks>
    /// Implementations should not interact with lifecycle callback methods on the <see cref="Actor" /> type.
    /// These methods will be called by the runtime.
    /// </remarks>
    public override Task<ActorActivatorState> CreateAsync(ActorHost host)
    {
        var type = host.ActorTypeInfo.ImplementationType;
        var actor = (Actor)Activator.CreateInstance(type, args: new object[]{ host, });
        return Task.FromResult(new ActorActivatorState(actor));
    }

    /// <summary>
    /// Deletes the actor instance and cleans up all associated resources.
    /// </summary>
    /// <param name="state">
    /// The <see cref="ActorActivatorState" /> instance that was created during creation of the actor.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous completion of the operation.
    /// </returns>
    /// <remarks>
    /// Implementations should not interact with lifecycle callback methods on the <see cref="Actor" /> type.
    /// These methods will be called by the runtime.
    /// </remarks>
    public async override Task DeleteAsync(ActorActivatorState state)
    {
        if (state.Actor is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync(); 
        }
        else if (state.Actor is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}