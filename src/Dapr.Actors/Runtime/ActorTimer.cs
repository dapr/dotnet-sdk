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
/// Represents the timer set on an Actor.
/// </summary>
public class ActorTimer : ActorTimerToken
{
    private static readonly TimeSpan MiniumPeriod = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Initializes a new instance of <see cref="ActorTimer" />.
    /// </summary>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="name">The timer name.</param>
    /// <param name="timerCallback">The name of the callback associated with the timer.</param>
    /// <param name="data">The state associated with the timer.</param>
    /// <param name="dueTime">The timer due time.</param>
    /// <param name="period">The timer period.</param>
    public ActorTimer(
        string actorType,
        ActorId actorId,
        string name,
        string timerCallback,
        byte[] data,
        TimeSpan dueTime,
        TimeSpan period)
        : this(new ActorTimerOptions
        {
            ActorTypeName = actorType,
            Id = actorId,
            TimerName = name,
            TimerCallback = timerCallback,
            Data = data,
            DueTime = dueTime,
            Period = period,
            Ttl = null
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ActorTimer" />.
    /// </summary>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="name">The timer name.</param>
    /// <param name="timerCallback">The name of the callback associated with the timer.</param>
    /// <param name="data">The state associated with the timer.</param>
    /// <param name="dueTime">The timer due time.</param>
    /// <param name="period">The timer period.</param>
    /// <param name="ttl">The timer ttl.</param>
    public ActorTimer(
        string actorType,
        ActorId actorId,
        string name,
        string timerCallback,
        byte[] data,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan ttl)
        : this(new ActorTimerOptions
        {
            ActorTypeName = actorType,
            Id = actorId,
            TimerName = name,
            TimerCallback = timerCallback,
            Data = data,
            DueTime = dueTime,
            Period = period,
            Ttl = ttl
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ActorTimer"/>.
    /// </summary>
    /// <param name="options">An <see cref="ActorTimerOptions"/> containing the various settings for an <see cref="ActorTimer"/>.</param>
    internal ActorTimer(ActorTimerOptions options) : base(options.ActorTypeName, options.Id, options.TimerName)
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

        this.TimerCallback = options.TimerCallback;
        this.Data = options.Data;
        this.DueTime = options.DueTime;
        this.Period = options.Period;
        this.Ttl = options.Ttl;
    }

    /// <summary>
    /// The constructor
    /// </summary>
    [Obsolete("This constructor does not provide all required data and should not be used.")]
    public ActorTimer(
        string timerName,
        TimerInfo timerInfo)
        : base("", ActorId.CreateRandom(), timerName)
    {
        this.TimerInfo = timerInfo;
    }

    /// <summary>
    /// Timer related information
    /// </summary>
#pragma warning disable 0618
    public TimerInfo TimerInfo { get; }
#pragma warning restore 0618


    /// <summary>
    /// Gets the callback name.
    /// </summary>
    public string TimerCallback { get; }

    /// <summary>
    /// Gets the state passed to the callback.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the time when the timer is first due to be invoked.
    /// </summary>
    public TimeSpan DueTime { get; }

    /// <summary>
    /// Gets the time interval at which the timer is invoked periodically.
    /// </summary>
    public TimeSpan Period { get; }

    /// <summary>
    /// The optional <see cref="TimeSpan"/> that states when the reminder will expire.
    /// </summary>
    public TimeSpan? Ttl { get; }
}