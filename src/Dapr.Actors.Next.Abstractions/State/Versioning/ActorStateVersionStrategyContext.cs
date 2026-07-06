namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Provides configuration context for an actor state versioning strategy instance.
/// </summary>
/// <param name="CanonicalName">The canonical actor state family name.</param>
/// <param name="OptionsName">The named options scope used to configure the strategy.</param>
public readonly record struct ActorStateVersionStrategyContext(string CanonicalName, string? OptionsName);
