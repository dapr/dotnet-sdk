// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.E2E.Test.Actors.Reminders
{
    public class State
    {
        public int Count { get; set; }

        public bool IsReminderRunning { get; set; }
    }
}
