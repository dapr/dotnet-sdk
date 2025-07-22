using System;
namespace Dapr.Actors.Runtime;

/// <summary>
/// A collection of options used to create an <see cref="ActorReminder"/>.
/// </summary>
internal class ActorReminderOptions
{
    /// <summary>
    /// The name of the type of the Actor that the reminder will fire for.
    /// </summary>
    public string ActorTypeName { get; set; }

    /// <summary>
    /// The <see cref="ActorId"/> that the reminder will fire for.
    /// </summary>
    public ActorId Id { get; set; }

    /// <summary>
    /// The name of the reminder.
    /// </summary>
    public string ReminderName { get; set; }

    /// <summary>
    /// State that is passed to the Actor when the reminder fires.
    /// </summary>
    public byte[] State { get; set; }

    /// <summary>
    /// <see cref="TimeSpan"/> that determines when the reminder will first fire.
    /// </summary>
    public TimeSpan DueTime { get; set; }

    /// <summary>
    /// <see cref="TimeSpan"/> that determines how much time there is between the reminder firing. Starts after the <see cref="DueTime"/>.
    /// </summary>
    public TimeSpan Period { get; set; }

    /// <summary>
    /// An optional <see cref="TimeSpan"/> that determines when the reminder will expire.
    /// </summary>
    public TimeSpan? Ttl { get; set; }
        
    /// <summary>
    /// The number of repetitions for which the reminder should be invoked.
    /// </summary>
    public int? Repetitions { get; set; }
}