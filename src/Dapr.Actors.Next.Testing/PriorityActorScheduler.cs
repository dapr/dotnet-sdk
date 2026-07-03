using Dapr.Actors.Next.Core.Scheduling;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Deterministic priority/PCT-style scheduler that assigns stable seeded priorities to actor mailboxes.
/// </summary>
public sealed class PriorityActorScheduler(int seed, int priorityChangeBound = 2) : ControlledActorScheduler(seed, "pct-priority")
{
    private readonly Random random = new(seed);
    private readonly Dictionary<string, int> priorities = new(StringComparer.Ordinal);
    private int remainingPriorityChanges = Math.Max(0, priorityChangeBound);

    /// <summary>
    /// Gets the maximum number of seeded priority changes this scheduler may introduce.
    /// </summary>
    public int PriorityChangeBound { get; } = Math.Max(0, priorityChangeBound);

    /// <inheritdoc />
    public override ControlledActorScheduler ReplayFromSeed() => new PriorityActorScheduler(Seed, PriorityChangeBound);

    /// <inheritdoc />
    protected override IControlledActorMailbox SelectMailbox(IReadOnlyList<IControlledActorMailbox> available)
    {
        foreach (var mailbox in available)
        {
            var key = Key(mailbox);
            if (!priorities.ContainsKey(key))
            {
                priorities[key] = random.Next();
            }
        }

        if (remainingPriorityChanges > 0 && available.Count > 1 && random.Next(4) == 0)
        {
            priorities[Key(available[random.Next(available.Count)])] = -random.Next(1, int.MaxValue);
            remainingPriorityChanges--;
        }

        return available
            .OrderBy(mailbox => priorities[Key(mailbox)])
            .ThenBy(mailbox => mailbox.ActorType, StringComparer.Ordinal)
            .ThenBy(mailbox => mailbox.ActorId.Value, StringComparer.Ordinal)
            .First();
    }

    private static string Key(IControlledActorMailbox mailbox) => $"{mailbox.ActorType}/{mailbox.ActorId.Value}";
}
