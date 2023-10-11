using Dapr.Actors;

namespace Dapr.E2E.Test.Actors.Generators;

public interface IRemotePingActor : IActor
{
    Task Ping();
}
