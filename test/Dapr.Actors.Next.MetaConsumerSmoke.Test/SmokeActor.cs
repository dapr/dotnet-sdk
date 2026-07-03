using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;

namespace Dapr.Actors.Next.MetaConsumerSmoke.Test;

[GenerateActorClient]
public interface ISmokeActor : IActor
{
    Task Ping(CancellationToken cancellationToken = default);
}

[DaprActor("SmokeActor")]
public sealed class SmokeActor : Actor, ISmokeActor
{
    protected override ActorId Id => ActorId.Create("unused");

    protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State =>
        throw new NotSupportedException();

    public Task Ping(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
