namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Identifies a concrete actor state versioning strategy type without exposing raw <see cref="Type"/> on options.
/// </summary>
public interface IActorStateVersionStrategyType
{
    /// <summary>
    /// Gets the concrete strategy type.
    /// </summary>
    Type StrategyType { get; }
}
