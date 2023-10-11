using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Generators;

internal sealed class RemoteActor : Actor, IRemoteActor
{
    private readonly ILogger<RemoteActor> logger;

    private RemoteState currentState = new("default");

    public RemoteActor(ActorHost host, ILogger<RemoteActor> logger)
        : base(host)
    {
        this.logger = logger;
    }

    public Task<RemoteState> GetState()
    {
        this.logger.LogInformation("GetStateAsync called.");

        return Task.FromResult(this.currentState);
    }

    public Task SetState(RemoteState state)
    {
        this.logger.LogInformation("SetStateAsync called.");

        this.currentState = state;

        return Task.CompletedTask;
    }

    public Task Ping()
    {
        return Task.CompletedTask;
    }
}