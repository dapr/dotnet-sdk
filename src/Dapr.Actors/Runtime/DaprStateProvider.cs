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

namespace Dapr.Actors.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Communication;

/// <summary>
/// State Provider to interact with Dapr runtime.
/// </summary>
internal class DaprStateProvider
{
    private readonly IActorStateSerializer actorStateSerializer;
    private readonly JsonSerializerOptions jsonSerializerOptions;

    private readonly IDaprInteractor daprInteractor;

    public DaprStateProvider(IDaprInteractor daprInteractor, IActorStateSerializer actorStateSerializer = null)
    {
        this.actorStateSerializer = actorStateSerializer;
        this.daprInteractor = daprInteractor;
    }

    public DaprStateProvider(IDaprInteractor daprInteractor, JsonSerializerOptions jsonSerializerOptions = null)
    {
        this.jsonSerializerOptions = jsonSerializerOptions;
        this.daprInteractor = daprInteractor;
    }

    public async Task<ConditionalValue<ActorStateResponse<T>>> TryLoadStateAsync<T>(string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
    {
        var result = new ConditionalValue<ActorStateResponse<T>>(false, default);
        var response = await this.daprInteractor.GetStateAsync(actorType, actorId, stateName, cancellationToken);

        if (response.Value.Length != 0 && (!response.TTLExpireTime.HasValue || response.TTLExpireTime.Value > DateTimeOffset.UtcNow))
        {
            T typedResult;

            // perform default json de-serialization if custom serializer was not provided.
            if (this.actorStateSerializer != null)
            {
                var byteResult = Convert.FromBase64String(response.Value.Trim('"'));
                typedResult = this.actorStateSerializer.Deserialize<T>(byteResult);
            }
            else
            {
                typedResult = JsonSerializer.Deserialize<T>(response.Value, jsonSerializerOptions);
            }

            result = new ConditionalValue<ActorStateResponse<T>>(true, new ActorStateResponse<T>(typedResult, response.TTLExpireTime));
        }

        return result;
    }

    public async Task<bool> ContainsStateAsync(string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
    {
        var result = await this.daprInteractor.GetStateAsync(actorType, actorId, stateName, cancellationToken);
        return (result.Value.Length != 0 && (!result.TTLExpireTime.HasValue || result.TTLExpireTime.Value > DateTimeOffset.UtcNow));
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
                        JsonSerializer.Serialize(writer, stateChange.Value, stateChange.Type, jsonSerializerOptions);
                    }

                    if (stateChange.TTLExpireTime.HasValue) {
                        var ttl = (int)Math.Ceiling((stateChange.TTLExpireTime.Value - DateTimeOffset.UtcNow).TotalSeconds);
                        writer.WritePropertyName("metadata");
                        writer.WriteStartObject();
                        writer.WriteString("ttlInSeconds", ttl.ToString());
                        writer.WriteEndObject();
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
        await this.daprInteractor.SaveStateTransactionallyAsync(actorType, actorId, content, cancellationToken);
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