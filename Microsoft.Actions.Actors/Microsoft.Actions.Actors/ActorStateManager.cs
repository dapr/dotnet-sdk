// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Runtime;

    internal sealed class ActorStateManager : IActorStateManager
    {
        private readonly Dictionary<string, StateMetadata> stateChangeTracker;
        private readonly ActorBase actor;

        public Task AddOrUpdateState<T>(string stateName, T value)
        {
            throw new System.NotImplementedException();
        }

        public Task ClearCacheAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        public Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        public Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        public Task SaveStateAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException();
        }

        private sealed class StateMetadata
        {
            private readonly Type type;

            private StateMetadata(object value, Type type, StateChangeKind changeKind)
            {
                this.Value = value;
                this.type = type;
                this.ChangeKind = changeKind;
            }

            public object Value { get; set; }

            public StateChangeKind ChangeKind { get; set; }

            public Type Type
            {
                get { return this.type; }
            }

            public static StateMetadata Create<T>(T value, StateChangeKind changeKind)
            {
                return new StateMetadata(value, typeof(T), changeKind);
            }

            public static StateMetadata CreateForRemove()
            {
                return new StateMetadata(null, typeof(object), StateChangeKind.Remove);
            }
        }
    }
}
