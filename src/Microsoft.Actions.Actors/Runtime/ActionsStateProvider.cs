// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// State Provider to interact with Actions runtime.
    /// </summary>
    public class ActionsStateProvider : IActorStateProvider
    {
        /// <inheritdoc/>
        public Task<T> LoadStateAsync<T>(ActorId actorId, string stateName, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public Task RemoveActorAsync(ActorId actorId, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public Task SaveStateAsync(ActorId actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }
    }
}
