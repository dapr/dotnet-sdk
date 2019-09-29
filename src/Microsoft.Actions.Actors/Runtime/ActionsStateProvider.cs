// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// State Provider to interact with Actions runtime.
    /// </summary>
    internal class ActionsStateProvider
    {
        private readonly ActorStateProviderSerializer actorStateSerializer;

        public ActionsStateProvider(ActorStateProviderSerializer actorStateSerializer)
        {
            this.actorStateSerializer = actorStateSerializer;
        }

        public async Task<ConditionalValue<T>> TryLoadStateAsync<T>(string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
        {
            var result = new ConditionalValue<T>(false, default);
            var stringResult = await ActorRuntime.ActionsInteractor.GetStateAsync(actorType, actorId, stateName);

            if (stringResult.Length != 0)
            {
                var byteResult = Convert.FromBase64String(stringResult.Trim('"'));
                var typedResult = this.actorStateSerializer.Deserialize<T>(byteResult);
                result = new ConditionalValue<T>(true, typedResult);
            }

            return result;
        }

        public async Task<bool> ContainsStateAsync(string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
        {
            var byteResult = await ActorRuntime.ActionsInteractor.GetStateAsync(actorType, actorId, stateName);
            return byteResult.Length != 0;
        }

        public async Task SaveStateAsync(string actorType, string actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default)
        {
            await this.DoStateChangesTransactionallyAsync(actorType, actorId, stateChanges, cancellationToken);
        }

        private Task DoStateChangesTransactionallyAsync(string actorType, string actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default)
        {
            // Transactional state update request body:
            /*
            [
                {
                    "operation": "upsert",
                    "request": {
                        "key": "key1",
                        "value": "myData"
                    }
                },
                {
                    "operation": "delete",
                    "request": {
                        "key": "key2"
                    }
                }
            ]
            */

            string content;
            using (var sw = new StringWriter())
            {
                var writer = new JsonTextWriter(sw);
                writer.WriteStartArray();

                foreach (var stateChange in stateChanges)
                {
                    writer.WriteStartObject();
                    var operation = this.GetActionsStateOperation(stateChange.ChangeKind);
                    writer.WriteProperty(operation, "operation", JsonWriterExtensions.WriteStringValue);
                    writer.WriteProperty(stateChange, "request", this.SerializeStateChangeRequest);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                content = sw.ToString();
            }

            return ActorRuntime.ActionsInteractor.SaveStateTransactionallyAsync(actorType, actorId, content, cancellationToken);
        }

        /// <summary>
        /// Save state individually. Actions runtime has added Tranaction save state. Use that instead. This method exists for debugging and testing of save state individually.
        /// </summary>
        private async Task DoStateChangesIndividuallyAsync(string actorType, string actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default)
        {
            foreach (var stateChange in stateChanges)
            {
                var keyName = stateChange.StateName;

                switch (stateChange.ChangeKind)
                {
                    case StateChangeKind.Remove:
                        await ActorRuntime.ActionsInteractor.RemoveStateAsync(actorType, actorId, keyName, cancellationToken);
                        break;
                    case StateChangeKind.Add:
                    case StateChangeKind.Update:
                        // Currently Actions runtime only support json serialization.
                        await ActorRuntime.ActionsInteractor.SaveStateAsync(actorType, actorId, keyName, JsonConvert.SerializeObject(stateChange.Value), cancellationToken);
                        break;
                    default:
                        break;
                }
            }
        }

        private string GetActionsStateOperation(StateChangeKind changeKind)
        {
            var operation = string.Empty;

            switch (changeKind)
            {
                case StateChangeKind.Remove:
                    operation = "delete";
                    break;
                case StateChangeKind.Add:
                case StateChangeKind.Update:
                    operation = "upsert";
                    break;
                default:
                    break;
            }

            return operation;
        }

        private void SerializeStateChangeRequest(JsonWriter writer, ActorStateChange stateChange)
        {
            writer.WriteStartObject();

            switch (stateChange.ChangeKind)
            {
                case StateChangeKind.Remove:
                    writer.WriteProperty(stateChange.StateName, "key", JsonWriterExtensions.WriteStringValue);
                    break;
                case StateChangeKind.Add:
                case StateChangeKind.Update:
                    writer.WriteProperty(stateChange.StateName, "key", JsonWriterExtensions.WriteStringValue);
                    var buffer = this.actorStateSerializer.Serialize(stateChange.Type, stateChange.Value);
                    writer.WriteProperty(Convert.ToBase64String(buffer), "value", JsonWriterExtensions.WriteStringValue);
                    break;
                default:
                    break;
            }

            writer.WriteEndObject();
        }
    }
}
