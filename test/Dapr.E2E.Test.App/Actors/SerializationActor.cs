
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Serialization;

public class SerializationActor : Actor, ISerializationActor
{
    public SerializationActor(ActorHost host)
        : base(host)
    {
    }

    public Task Ping()
    {
        return Task.CompletedTask;
    }

    public Task<SerializationPayload> SendAsync(string name, 
        SerializationPayload payload, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(payload);
    }

    public Task<DateTime> AnotherMethod(DateTime payload){
        return Task.FromResult(payload);
    }
}