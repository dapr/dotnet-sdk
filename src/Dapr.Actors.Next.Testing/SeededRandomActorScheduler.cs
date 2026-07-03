using Dapr.Actors.Next.Core.Scheduling;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Deterministic scheduler that chooses among executable mailboxes using a seeded random source.
/// </summary>
public sealed class SeededRandomActorScheduler(int seed) : ControlledActorScheduler(seed, "seeded-random")
{
    private readonly Random random = new(seed);

    /// <inheritdoc />
    public override ControlledActorScheduler ReplayFromSeed() => new SeededRandomActorScheduler(Seed);

    /// <inheritdoc />
    protected override IControlledActorMailbox SelectMailbox(IReadOnlyList<IControlledActorMailbox> available) =>
        available[random.Next(available.Count)];
}
