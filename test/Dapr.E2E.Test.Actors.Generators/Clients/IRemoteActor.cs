namespace Dapr.E2E.Test.Actors.Generators.Clients;

public record RemoteState(string Value);

public interface IRemoteActor : IPingActor
{
    Task<RemoteState> GetState();

    Task SetState(RemoteState state);
}