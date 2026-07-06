namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Identifies a concrete actor state versioning strategy type.
/// </summary>
/// <typeparam name="TStrategy">The concrete actor state versioning strategy type.</typeparam>
public sealed class ActorStateVersionStrategyType<TStrategy> : IActorStateVersionStrategyType
    where TStrategy : class, IActorStateVersionStrategy
{
    private ActorStateVersionStrategyType()
    {
    }

    /// <summary>
    /// Gets the singleton strategy type identifier.
    /// </summary>
    public static ActorStateVersionStrategyType<TStrategy> Instance { get; } = new();

    /// <inheritdoc />
    public Type StrategyType => typeof(TStrategy);
}
