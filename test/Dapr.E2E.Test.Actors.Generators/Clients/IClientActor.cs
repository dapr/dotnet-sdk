using Dapr.Actors.Generators;

namespace Dapr.E2E.Test.Actors.Generators.Clients;

internal record ClientState(string Value);

[GenerateActorClient]
internal interface IClientActor
{
    [ActorMethod(Name = "GetState")]
    Task<ClientState> GetStateAsync(CancellationToken cancellationToken = default);

    [ActorMethod(Name = "SetState")]
    Task SetStateAsync(ClientState state, CancellationToken cancellationToken = default);
}
