namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Consumes actor state versioning strategy context after construction.
/// </summary>
public interface IActorStateVersionStrategyContextConsumer
{
    /// <summary>
    /// Configures the strategy for a canonical actor state family.
    /// </summary>
    void Configure(ActorStateVersionStrategyContext context);
}
