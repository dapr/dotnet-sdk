using Dapr.Actors.Runtime;

namespace GeneratedActor;

internal sealed class MyPublicActor : Actor, IMyPublicActor
{
    private readonly ILogger<MyPublicActor> logger;

    private MyState currentState = new("default");

    public MyPublicActor(ActorHost host, ILogger<MyPublicActor> logger)
        : base(host)
    {
        this.logger = logger;
    }

    #region  IMyPublicActor Members

    public Task<MyState> GetStateAsync()
    {
        this.logger.LogInformation("GetStateAsync called.");

        return Task.FromResult(this.currentState);
    }

    public Task SetStateAsync(MyState state)
    {
        this.logger.LogInformation("SetStateAsync called.");

        this.currentState = state;

        return Task.CompletedTask;
    }

    #endregion
}