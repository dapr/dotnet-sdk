namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Selects the persisted form written by <see cref="ActorTestRuntime.SeedStateAsync{T}(string, Dapr.Actors.Next.Abstractions.ActorId, string, T, ActorStateSeedForm, CancellationToken)"/>.
/// </summary>
public enum ActorStateSeedForm
{
    /// <summary>
    /// Seed an enrolled state value with the migration discriminator for the seeded value's type.
    /// </summary>
    Enveloped,

    /// <summary>
    /// Seed a graduated plain state value with the SDK state header and no migration discriminator.
    /// </summary>
    Plain,
}
