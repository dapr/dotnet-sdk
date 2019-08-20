// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{    
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// State Provider to interact with Actions runtime.
    /// </summary>
    internal static class ActionsStateProvider
    {
        private static IActionsInteractor actionsInteractor = new ActionsHttpInteractor();

        public static async Task<ConditionalValue<T>> TryLoadStateAsync<T>(string actorType, string actorId, string stateName, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = new ConditionalValue<T>(false, default(T));
            var byteResult = await actionsInteractor.GetStateAsync(actorType, actorId, stateName);            

            if (byteResult.Length != 0)
            {
                // Currently Actions runtime only support json serialization.
                var state = Encoding.UTF8.GetString(byteResult);
                var typedResult = JsonConvert.DeserializeObject<T>(state);
                result = new ConditionalValue<T>(true, typedResult);
            }

            return result;
        }

        public static async Task<bool> ContainsStateAsync(string actorType, string actorId, string stateName, CancellationToken cancellationToken = default(CancellationToken))
        {
            var byteResult = await actionsInteractor.GetStateAsync(actorType, actorId, stateName);
            return byteResult.Length != 0;
        }

        public static async Task SaveStateAsync(string actorType, string actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Save state individually as Transactional update is not yet supported by Actions runtime.
            foreach (var stateChange in stateChanges)
            {
                var keyName = stateChange.StateName;

                switch (stateChange.ChangeKind)
                {
                    case StateChangeKind.Remove:
                        await actionsInteractor.RemoveStateAsync(actorType, actorId, keyName, cancellationToken);
                        break;
                    case StateChangeKind.Add:
                    case StateChangeKind.Update:
                        // Currently Actions runtime only support json serialization.
                        await actionsInteractor.SaveStateAsync(actorType, actorId, keyName, JsonConvert.SerializeObject(stateChange.Value), cancellationToken);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
