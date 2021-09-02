// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Timeout
{
    public class TimeoutActor : Actor, ITimeoutActor
    {
        public TimeoutActor(ActorHost host) : base(host)
        {            
        }

        public Task Ping()
        {
            return Task.CompletedTask;
        }

        public Task TimeoutCall(TimeoutCallOptions callOptions)
        {
            Thread.Sleep(TimeSpan.FromSeconds(callOptions.SleepTime));
            return Task.CompletedTask;
        }
    }
}