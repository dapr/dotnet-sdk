// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.E2E.Test.Actors
{
    public interface IPingActor : IActor
    {
        Task Ping();
    }
}
