// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors.Resources;

    internal sealed class ActorStateManager : IActorStateManager
    {
        private readonly Actor actor;
        private readonly string actorType;
        private readonly Dictionary<string, StateMetadata> stateChangeTracker;

        internal ActorStateManager(Actor actor)
        {
            this.actor = actor;
            this.actorType = actor.ActorService.ActorTypeInfo.ActorTypeName;
            this.stateChangeTracker = new Dictionary<string, StateMetadata>();
        }

        public async Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken)
        {
            if (!(await this.TryAddStateAsync(stateName, value, cancellationToken)))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, SR.ActorStateAlreadyExists, stateName));
            }
        }

        public async Task<bool> TryAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken)
        {
            ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

            if (this.stateChangeTracker.ContainsKey(stateName))
            {
                var stateMetadata = this.stateChangeTracker[stateName];

                // Check if the property was marked as remove in the cache
                if (stateMetadata.ChangeKind == StateChangeKind.Remove)
                {
                    this.stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Update);
                    return true;
                }

                return false;
            }

            if (await this.actor.ActorService.StateProvider.ContainsStateAsync(this.actorType, this.actor.Id.ToString(), stateName, cancellationToken))
            {
                return false;
            }

            this.stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Add);
            return true;
        }

        public async Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken)
        {
            var condRes = await this.TryGetStateAsync<T>(stateName, cancellationToken);

            if (condRes.HasValue)
            {
                return condRes.Value;
            }

            throw new KeyNotFoundException(string.Format(CultureInfo.CurrentCulture, SR.ErrorNamedActorStateNotFound, stateName));
        }

        public async Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken)
        {
            ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));
            if (this.stateChangeTracker.ContainsKey(stateName))
            {
                var stateMetadata = this.stateChangeTracker[stateName];

                // Check if the property was marked as remove in the cache
                if (stateMetadata.ChangeKind == StateChangeKind.Remove)
                {
                    return new ConditionalValue<T>(false, default);
                }

                return new ConditionalValue<T>(true, (T)stateMetadata.Value);
            }

            var conditionalResult = await this.TryGetStateFromStateProviderAsync<T>(stateName, cancellationToken);
            if (conditionalResult.HasValue)
            {
                this.stateChangeTracker.Add(stateName, StateMetadata.Create(conditionalResult.Value, StateChangeKind.None));
            }

            return conditionalResult;
        }

        public async Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken)
        {
            ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

            if (this.stateChangeTracker.ContainsKey(stateName))
            {
                var stateMetadata = this.stateChangeTracker[stateName];
                stateMetadata.Value = value;

                if (stateMetadata.ChangeKind == StateChangeKind.None ||
                    stateMetadata.ChangeKind == StateChangeKind.Remove)
                {
                    stateMetadata.ChangeKind = StateChangeKind.Update;
                }
            }
            else if (await this.actor.ActorService.StateProvider.ContainsStateAsync(this.actorType, this.actor.Id.ToString(), stateName, cancellationToken))
            {
                this.stateChangeTracker.Add(stateName, StateMetadata.Create(value, StateChangeKind.Update));
            }
            else
            {
                this.stateChangeTracker[stateName] = StateMetadata.Create(value, StateChangeKind.Add);
            }
        }

        public async Task RemoveStateAsync(string stateName, CancellationToken cancellationToken)
        {
            if (!(await this.TryRemoveStateAsync(stateName, cancellationToken)))
            {
                throw new KeyNotFoundException(string.Format(CultureInfo.CurrentCulture, SR.ErrorNamedActorStateNotFound, stateName));
            }
        }

        public async Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken)
        {
            ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

            if (this.stateChangeTracker.ContainsKey(stateName))
            {
                var stateMetadata = this.stateChangeTracker[stateName];

                switch (stateMetadata.ChangeKind)
                {
                    case StateChangeKind.Remove:
                        return false;
                    case StateChangeKind.Add:
                        this.stateChangeTracker.Remove(stateName);
                        return true;
                }

                stateMetadata.ChangeKind = StateChangeKind.Remove;
                return true;
            }

            if (await this.actor.ActorService.StateProvider.ContainsStateAsync(this.actorType, this.actor.Id.ToString(), stateName, cancellationToken))
            {
                this.stateChangeTracker.Add(stateName, StateMetadata.CreateForRemove());
                return true;
            }

            return false;
        }

        public async Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken)
        {
            ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

            if (this.stateChangeTracker.ContainsKey(stateName))
            {
                var stateMetadata = this.stateChangeTracker[stateName];

                // Check if the property was marked as remove in the cache
                return stateMetadata.ChangeKind != StateChangeKind.Remove;
            }

            if (await this.actor.ActorService.StateProvider.ContainsStateAsync(this.actorType, this.actor.Id.ToString(), stateName, cancellationToken))
            {
                return true;
            }

            return false;
        }

        public async Task<T> GetOrAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken)
        {
            var condRes = await this.TryGetStateAsync<T>(stateName, cancellationToken);

            if (condRes.HasValue)
            {
                return condRes.Value;
            }

            var changeKind = this.IsStateMarkedForRemove(stateName) ? StateChangeKind.Update : StateChangeKind.Add;

            this.stateChangeTracker[stateName] = StateMetadata.Create(value, changeKind);
            return value;
        }

        public async Task<T> AddOrUpdateStateAsync<T>(
            string stateName,
            T addValue,
            Func<string, T, T> updateValueFactory,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

            if (this.stateChangeTracker.ContainsKey(stateName))
            {
                var stateMetadata = this.stateChangeTracker[stateName];

                // Check if the property was marked as remove in the cache
                if (stateMetadata.ChangeKind == StateChangeKind.Remove)
                {
                    this.stateChangeTracker[stateName] = StateMetadata.Create(addValue, StateChangeKind.Update);
                    return addValue;
                }

                var newValue = updateValueFactory.Invoke(stateName, (T)stateMetadata.Value);
                stateMetadata.Value = newValue;

                if (stateMetadata.ChangeKind == StateChangeKind.None)
                {
                    stateMetadata.ChangeKind = StateChangeKind.Update;
                }

                return newValue;
            }

            var conditionalResult = await this.TryGetStateFromStateProviderAsync<T>(stateName, cancellationToken);
            if (conditionalResult.HasValue)
            {
                var newValue = updateValueFactory.Invoke(stateName, conditionalResult.Value);
                this.stateChangeTracker.Add(stateName, StateMetadata.Create(newValue, StateChangeKind.Update));

                return newValue;
            }

            this.stateChangeTracker[stateName] = StateMetadata.Create(addValue, StateChangeKind.Add);
            return addValue;
        }

        public async Task<IEnumerable<string>> GetStateNamesAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Get all state names from Dapr once implemented.
            // var namesFromStateProvider = await this.stateProvider.EnumerateStateNamesAsync(this.actor.Id, cancellationToken);
            await Task.CompletedTask;
            var stateNameList = new List<string>();

            var kvPairEnumerator = this.stateChangeTracker.GetEnumerator();

            while (kvPairEnumerator.MoveNext())
            {
                switch (kvPairEnumerator.Current.Value.ChangeKind)
                {
                    case StateChangeKind.Add:
                        stateNameList.Add(kvPairEnumerator.Current.Key);
                        break;
                    case StateChangeKind.Remove:
                        stateNameList.Remove(kvPairEnumerator.Current.Key);
                        break;
                }
            }

            return stateNameList;
        }

        public Task ClearCacheAsync(CancellationToken cancellationToken)
        {
            this.stateChangeTracker.Clear();
            return Task.CompletedTask;
        }

        public async Task SaveStateAsync(CancellationToken cancellationToken = default)
        {
            if (this.stateChangeTracker.Count > 0)
            {
                var stateChangeList = new List<ActorStateChange>();
                var statesToRemove = new List<string>();

                foreach (var stateName in this.stateChangeTracker.Keys)
                {
                    var stateMetadata = this.stateChangeTracker[stateName];

                    if (stateMetadata.ChangeKind != StateChangeKind.None)
                    {
                        stateChangeList.Add(
                            new ActorStateChange(stateName, stateMetadata.Type, stateMetadata.Value, stateMetadata.ChangeKind));

                        if (stateMetadata.ChangeKind == StateChangeKind.Remove)
                        {
                            statesToRemove.Add(stateName);
                        }

                        // Mark the states as unmodified so that tracking for next invocation is done correctly.
                        stateMetadata.ChangeKind = StateChangeKind.None;
                    }
                }

                if (stateChangeList.Count > 0)
                {
                    await this.actor.ActorService.StateProvider.SaveStateAsync(this.actorType, this.actor.Id.ToString(), stateChangeList.AsReadOnly(), cancellationToken);
                }

                // Remove the states from tracker whcih were marked for removal.
                foreach (var stateToRemove in statesToRemove)
                {
                    this.stateChangeTracker.Remove(stateToRemove);
                }
            }
        }

        private bool IsStateMarkedForRemove(string stateName)
        {
            if (this.stateChangeTracker.ContainsKey(stateName) &&
                this.stateChangeTracker[stateName].ChangeKind == StateChangeKind.Remove)
            {
                return true;
            }

            return false;
        }

        private Task<ConditionalValue<T>> TryGetStateFromStateProviderAsync<T>(string stateName, CancellationToken cancellationToken)
        {
            return this.actor.ActorService.StateProvider.TryLoadStateAsync<T>(this.actorType, this.actor.Id.ToString(), stateName, cancellationToken);
        }

        private sealed class StateMetadata
        {
            private StateMetadata(object value,     Type type, StateChangeKind changeKind)
            {
                this.Value = value;
                this.Type = type;
                this.ChangeKind = changeKind;
            }

            public object Value { get; set; }

            public StateChangeKind ChangeKind { get; set; }

            public Type Type { get; }

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
