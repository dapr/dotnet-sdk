// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the timer set on an Actor.
    /// </summary>
    public class ActorTimer
    {
        private readonly Actor owner;

        /// <summary>
        /// The constructor
        /// </summary>
        public ActorTimer(
            Actor owner,
            string timerName,
            TimerInfo timerInfo)
        {
            this.owner = owner;
            this.Name = timerName;
            this.TimerInfo = timerInfo;
        }

        /// <summary>
        /// Timer name
        /// </summary>
        public string Name { get; }

        internal TimerInfo TimerInfo { get; }


        /// <summary>
        /// Gets the callback routine to be invoked when the timer is fired
        /// </summary>
        public string TimerCallback
        {
            get { return this.TimerInfo.Callback; }
        }

        /// <summary>
        /// Parameters to be passed in to the timer callback routine
        /// </summary>
        public byte[] Data
        {
            get { return this.TimerInfo.Data; }
        }

        /// <summary>
        /// Gets the time when the timer is first due to be invoked.
        /// </summary>
        public TimeSpan DueTime
        {
            get { return this.TimerInfo.DueTime; }
        }

        /// <summary>
        /// Gets the time interval at which the timer is invoked periodically.
        /// </summary>
        public TimeSpan Period
        {
            get { return this.TimerInfo.Period; }
        }
    }
}
