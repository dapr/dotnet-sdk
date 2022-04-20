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

namespace Dapr.Actors.Runtime
{
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
            : base(actorType, actorId, name)
        {
            if (dueTime < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTime), string.Format(
                    CultureInfo.CurrentCulture,
                    SR.TimerArgumentOutOfRange,
                    TimeSpan.Zero.TotalMilliseconds,
                    TimeSpan.MaxValue.TotalMilliseconds));
            }

            if (period < MiniumPeriod)
            {
                throw new ArgumentOutOfRangeException(nameof(period), string.Format(
                    CultureInfo.CurrentCulture,
                    SR.TimerArgumentOutOfRange,
                    MiniumPeriod.TotalMilliseconds,
                    TimeSpan.MaxValue.TotalMilliseconds));
            }

            this.State = state;
            this.DueTime = dueTime;
            this.Period = period;
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
    }
}
