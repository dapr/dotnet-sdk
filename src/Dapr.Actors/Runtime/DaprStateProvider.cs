﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// State Provider to interact with Dapr runtime.
    /// </summary>
    internal class DaprStateProvider
    {
        private readonly IActorStateSerializer actorStateSerializer;

        public DaprStateProvider(IActorStateSerializer actorStateSerializer = null)
        {
            this.actorStateSerializer = actorStateSerializer;
        }

        public async Task<ConditionalValue<T>> TryLoadStateAsync<T>(string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
        {
            var result = new ConditionalValue<T>(false, default);
            var stringResult = await ActorRuntime.DaprInteractor.GetStateAsync(actorType, actorId, stateName, cancellationToken);

            if (stringResult.Length != 0)
            {
                T typedResult;

                // perform default json de-serialization if custom serializer was not provided.
                if (this.actorStateSerializer != null)
                {
                    var byteResult = Convert.FromBase64String(stringResult.Trim('"'));
                    typedResult = this.actorStateSerializer.Deserialize<T>(byteResult);
                }
                else
                {
                    typedResult = JsonSerializer.Deserialize<T>(stringResult);
                }

                result = new ConditionalValue<T>(true, typedResult);
            }

            return result;
        }

        public async Task<bool> ContainsStateAsync(string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
        {
            var byteResult = await ActorRuntime.DaprInteractor.GetStateAsync(actorType, actorId, stateName, cancellationToken);
            return byteResult.Length != 0;
        }

        public async Task SaveStateAsync(string actorType, string actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default)
        {
            await this.DoStateChangesTransactionallyAsync(actorType, actorId, stateChanges, cancellationToken);
        }

        private async Task DoStateChangesTransactionallyAsync(string actorType, string actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default)
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
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartArray();
            foreach (var stateChange in stateChanges)
            {
                writer.WriteStartObject();
                var operation = this.GetDaprStateOperation(stateChange.ChangeKind);
                writer.WriteString("operation", operation);

                // write the requestProperty
                writer.WritePropertyName("request");
                writer.WriteStartObject();  // start object for request property
                switch (stateChange.ChangeKind)
                {
                    case StateChangeKind.Remove:
                        writer.WriteString("key", stateChange.StateName);
                        break;
                    case StateChangeKind.Add:
                    case StateChangeKind.Update:
                        writer.WriteString("key", stateChange.StateName);

                        // perform default json serialization if custom serializer was not provided.
                        if (this.actorStateSerializer != null)
                        {
                            var buffer = this.actorStateSerializer.Serialize(stateChange.Type, stateChange.Value);
                            writer.WriteBase64String("value", buffer);
                        }
                        else
                        {
                            writer.WritePropertyName("value");
                            JsonSerializer.Serialize(writer, stateChange.Value, stateChange.Type);
                        }
                        break;
                    default:
                        break;
                }

                writer.WriteEndObject();  // end object for request property
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            await writer.FlushAsync();
            var content = Encoding.UTF8.GetString(stream.ToArray());
            await ActorRuntime.DaprInteractor.SaveStateTransactionallyAsync(actorType, actorId, content, cancellationToken);
        }

        private string GetDaprStateOperation(StateChangeKind changeKind)
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
    }
}
