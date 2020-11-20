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

    internal class ActorTimer : IActorTimer
    {
        private readonly Actor owner;

        public ActorTimer(
            Actor owner,
            string timerName,
            TimerInfo timerInfo)
        {
            this.owner = owner;
            this.Name = timerName;
            this.TimerInfo = timerInfo;
        }

        public string Name { get; }

        internal TimerInfo TimerInfo { get; }


        public string TimerCallback
        {
            get { return this.TimerInfo.Callback; }
        }

        public byte[] State
        {
            get { return this.TimerInfo.Data; }
        }

        public TimeSpan DueTime
        {
            get { return this.TimerInfo.DueTime; }
        }

        public TimeSpan Period
        {
            get { return this.TimerInfo.Period; }
        }

        internal ActorId OwnerActorId { get; }
    }
}
