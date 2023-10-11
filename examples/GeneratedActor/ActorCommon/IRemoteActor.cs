using Dapr.Actors;

namespace GeneratedActor;

public sealed record RemoteState(string Value);

public interface IRemoteActor : IActor
{
    Task<RemoteState> GetState();

    Task SetState(RemoteState state);
}
