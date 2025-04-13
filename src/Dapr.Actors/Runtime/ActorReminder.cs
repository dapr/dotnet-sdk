// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Threading;
using Dapr.Actors.Resources;

namespace Dapr.Actors.Runtime;

/// <summary>
/// Represents a reminder registered by an actor.
/// </summary>
public class ActorReminder : ActorReminderToken, IActorReminder
{
    private static readonly TimeSpan MiniumPeriod = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Initializes a new instance of <see cref="ActorReminder" />.
    /// </summary>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="name">The reminder name.</param>
    /// <param name="state">The state associated with the reminder.</param>
    /// <param name="dueTime">The reminder due time.</param>
    /// <param name="period">The reminder period.</param>
    public ActorReminder(
        string actorType,
        ActorId actorId,
        string name,
        byte[] state,
        TimeSpan dueTime,
        TimeSpan period)
        : this(new ActorReminderOptions
        {
            ActorTypeName = actorType,
            Id = actorId,
            ReminderName = name,
            State = state,
            DueTime = dueTime,
            Period = period,
            Ttl = null
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ActorReminder" />.
    /// </summary>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="name">The reminder name.</param>
    /// <param name="state">The state associated with the reminder.</param>
    /// <param name="dueTime">The reminder due time.</param>
    /// <param name="period">The reminder period.</param>
    /// <param name="ttl">The reminder ttl.</param>
    public ActorReminder(
        string actorType,
        ActorId actorId,
        string name,
        byte[] state,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan ttl)
        : this(new ActorReminderOptions
        {
            ActorTypeName = actorType,
            Id = actorId,
            ReminderName = name,
            State = state,
            DueTime = dueTime,
            Period = period,
            Ttl = ttl
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ActorReminder" />.
    /// </summary>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="name">The reminder name.</param>
    /// <param name="state">The state associated with the reminder.</param>
    /// <param name="dueTime">The reminder due time.</param>
    /// <param name="period">The reminder period.</param>
    /// <param name="repetitions">The number of times reminder should be invoked.</param>
    /// <param name="ttl">The reminder ttl.</param>
    public ActorReminder(
        string actorType,
        ActorId actorId,
        string name,
        byte[] state,
        TimeSpan dueTime,
        TimeSpan period,
        int? repetitions,
        TimeSpan? ttl)
        : this(new ActorReminderOptions
        {
            ActorTypeName = actorType,
            Id = actorId,
            ReminderName = name,
            State = state,
            DueTime = dueTime,
            Period = period,
            Repetitions = repetitions,
            Ttl = ttl
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ActorReminder" />.
    /// </summary>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="name">The reminder name.</param>
    /// <param name="state">The state associated with the reminder.</param>
    /// <param name="dueTime">The reminder due time.</param>
    /// <param name="period">The reminder period.</param>
    /// <param name="repetitions">The number of times reminder should be invoked.</param>
    public ActorReminder(
        string actorType,
        ActorId actorId,
        string name,
        byte[] state,
        TimeSpan dueTime,
        TimeSpan period,
        int? repetitions)
        : this(new ActorReminderOptions
        {
            ActorTypeName = actorType,
            Id = actorId,
            ReminderName = name,
            State = state,
            DueTime = dueTime,
            Period = period,
            Repetitions = repetitions
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ActorReminder" />.
    /// </summary>
    /// <param name="options">A <see cref="ActorReminderOptions" /> containing the various settings for an <see cref="ActorReminder"/>.</param>
    internal ActorReminder(ActorReminderOptions options)
        : base(options.ActorTypeName, options.Id, options.ReminderName)
    {
        if (options.DueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.DueTime), string.Format(
                CultureInfo.CurrentCulture,
                SR.TimerArgumentOutOfRange,
                TimeSpan.Zero.TotalMilliseconds,
                TimeSpan.MaxValue.TotalMilliseconds));
        }

        if (options.Period < MiniumPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Period), string.Format(
                CultureInfo.CurrentCulture,
                SR.TimerArgumentOutOfRange,
                MiniumPeriod.TotalMilliseconds,
                TimeSpan.MaxValue.TotalMilliseconds));
        }

        if (options.Ttl != null && (options.Ttl < options.DueTime || options.Ttl < TimeSpan.Zero))
        {
            throw new ArgumentOutOfRangeException(nameof(options.Ttl), string.Format(
                CultureInfo.CurrentCulture,
                SR.TimerArgumentOutOfRange,
                options.DueTime,
                TimeSpan.MaxValue.TotalMilliseconds));
        }
            
        if (options.Repetitions != null && options.Repetitions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Repetitions), string.Format(
                CultureInfo.CurrentCulture,
                SR.RepetitionsArgumentOutOfRange,
                options.Repetitions));
        }

        this.State = options.State;
        this.DueTime = options.DueTime;
        this.Period = options.Period;
        this.Ttl = options.Ttl;
        this.Repetitions = options.Repetitions;
    }

    /// <summary>
    /// Gets the reminder state.
    /// </summary>
    public byte[] State { get; }

    /// <summary>
    /// Gets the reminder due time.
    /// </summary>
    public TimeSpan DueTime { get; }

    /// <summary>
    /// Gets the reminder period.
    /// </summary>
    public TimeSpan Period { get; }

    /// <summary>
    /// The optional <see cref="TimeSpan"/> that states when the reminder will expire.
    /// </summary>
    public TimeSpan? Ttl { get; }
        
    /// <summary>
    /// The optional property that gets the number of invocations of the reminder left.
    /// </summary>
    public int? Repetitions { get; }
}