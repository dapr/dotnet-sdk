// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.E2E.Test.Actors.Timers
{
    public interface ITimerActor : IPingActor, IActor
    {
        Task StartTimer(StartTimerOptions options);

        Task<State> GetState();
    }
}
