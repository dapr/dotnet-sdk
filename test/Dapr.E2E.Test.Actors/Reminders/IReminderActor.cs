// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.E2E.Test.Actors.Reminders
{
    public interface IReminderActor : IPingActor, IActor
    {
        Task StartTimer(StartReminderOptions options);

        Task<State> GetState();
    }
}
