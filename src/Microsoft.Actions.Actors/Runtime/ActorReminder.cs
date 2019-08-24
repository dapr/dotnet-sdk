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
            ReminderData reminderData)
        {
            this.OwnerActorId = actorId;
            this.Name = reminderName;
            this.ReminderData = reminderData;
        }

        public string Name { get; }

        public byte[] State
        {
            get { return this.ReminderData.Data; }
        }

        public TimeSpan DueTime
        {
            get { return this.ReminderData.DueTime; }
        }

        public TimeSpan Period
        {
            get { return this.ReminderData.Period; }
        }

        internal ReminderData ReminderData { get; }

        internal ActorId OwnerActorId { get; }
    }
}
