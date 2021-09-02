// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.E2E.Test.Actors.Timeout
{
    public interface ITimeoutActor : IPingActor, IActor
    {
        Task TimeoutCall(TimeoutCallOptions callOptions);
    }
}