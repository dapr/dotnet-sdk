using System;
namespace Dapr.Actors.Runtime;

/// <summary>
/// Collection of all the options used for creating a <see cref="ActorTimer"/>.
/// </summary>
internal struct ActorTimerOptions
{
    /// <summary>
    /// The name of the type of the Actor that the timer will fire for.
    /// </summary>
    public string ActorTypeName { get; set; }

    /// <summary>
    /// The <see cref="ActorId"/> that the timer will fire for.
    /// </summary>
    public ActorId Id { get; set; }

    /// <summary>
    /// The name of the timer.
    /// </summary>
    public string TimerName { get; set; }

    /// <summary>
    /// The name of the callback for the timer.
    /// </summary>
    public string TimerCallback { get; set; }

    /// <summary>
    /// State that is passed to the Actor when the timer fires.
    /// </summary>
    public byte[] Data { get; set; }

    /// <summary>
    /// <see cref="TimeSpan"/> that determines when the timer will first fire.
    /// </summary>
    public TimeSpan DueTime { get; set; }

    /// <summary>
    /// <see cref="TimeSpan"/> that determines how much time there is between the timer firing. Starts after the <see cref="DueTime"/>.
    /// </summary>
    public TimeSpan Period { get; set; }

    /// <summary>
    /// An optional <see cref="TimeSpan"/> that determines when the timer will expire.
    /// </summary>
    public TimeSpan? Ttl { get; set; }
}