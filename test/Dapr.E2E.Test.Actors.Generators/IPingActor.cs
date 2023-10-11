using Dapr.Actors;

namespace Dapr.E2E.Test.Actors.Generators;

public interface IPingActor : IActor
{
    Task Ping();
}
