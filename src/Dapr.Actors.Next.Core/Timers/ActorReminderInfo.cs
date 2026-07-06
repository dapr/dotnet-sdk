using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Describes a durable actor reminder registration.
/// </summary>
public sealed record ActorReminderInfo(
    string ActorType,
    ActorId ActorId,
    TimeSpan? DueTime,
    TimeSpan? Period,
    string? ArgumentsJson,
    TimeSpan? Ttl);

/// <summary>
/// Describes a named durable actor reminder registration returned from a list operation.
/// </summary>
public sealed record NamedActorReminderInfo(string Name, ActorReminderInfo Reminder);
