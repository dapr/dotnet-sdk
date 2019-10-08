// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Runtime
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ActorReminder : IActorReminder
    {
        public ActorReminder(
            ActorId actorId,
            string reminderName,
            ReminderInfo reminderInfo)
        {
            this.OwnerActorId = actorId;
            this.Name = reminderName;
            this.ReminderInfo = reminderInfo;
        }

        public string Name { get; }

        public byte[] State
        {
            get { return this.ReminderInfo.Data; }
        }

        public TimeSpan DueTime
        {
            get { return this.ReminderInfo.DueTime; }
        }

        public TimeSpan Period
        {
            get { return this.ReminderInfo.Period; }
        }

        internal ReminderInfo ReminderInfo { get; }

        internal ActorId OwnerActorId { get; }
    }
}
