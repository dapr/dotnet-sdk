using Dapr.Actors;
using Dapr.Actors.Client;

namespace GeneratedActor;

public sealed class MyPublicActorManualProxy : IMyPublicActor
{
    private readonly ActorProxy actorProxy;

    public MyPublicActorManualProxy(ActorProxy actorProxy)
    {
        this.actorProxy = actorProxy;
    }

    public Task<MyState> GetStateAsync()
    {
        return this.actorProxy.InvokeMethodAsync<MyState>("GetStateAsync");
    }

    public Task SetStateAsync(MyState state)
    {
        return this.actorProxy.InvokeMethodAsync("SetStateAsync", state);
    }
}
