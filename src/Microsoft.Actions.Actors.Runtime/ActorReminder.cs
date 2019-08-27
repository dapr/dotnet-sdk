// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
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
