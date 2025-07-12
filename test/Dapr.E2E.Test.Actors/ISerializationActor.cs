using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.E2E.Test.Actors;

public interface ISerializationActor : IActor, IPingActor
{
    Task<SerializationPayload> SendAsync(string name, SerializationPayload payload, CancellationToken cancellationToken = default);
    Task<DateTime> AnotherMethod(DateTime payload);
}

public record SerializationPayload(string Message)
{
    public JsonElement Value { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> ExtensionData { get; set; }
}