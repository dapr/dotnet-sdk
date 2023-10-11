using Dapr.Actors;
using Dapr.Actors.Client;

namespace Dapr.E2E.Test.Actors.Generators;

internal static class ActorState
{
    public static async Task EnsureReadyAsync<TActor>(ActorId actorId, string actorType, ActorProxyOptions? options = null, CancellationToken cancellationToken = default)
        where TActor : IPingActor
    {
        var pingProxy = ActorProxy.Create<TActor>(actorId, actorType, options);

        while (true)
        {
            try
            {
                await pingProxy.Ping();

                break;
            }
            catch (DaprApiException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }
    }
}
