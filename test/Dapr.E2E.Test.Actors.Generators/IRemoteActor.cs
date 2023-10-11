using Dapr.Actors;

namespace Dapr.E2E.Test.Actors.Generators;

public record RemoteState(string Value);

public interface IRemoteActor : IActor
{
    Task<RemoteState> GetState();

    Task SetState(RemoteState state);
}