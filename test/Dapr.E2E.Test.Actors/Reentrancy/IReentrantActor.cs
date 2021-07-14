// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.E2E.Test.Actors.Reentrancy
{
    public interface IReentrantActor : IPingActor, IActor
    {
        Task ReentrantCall(ReentrantCallOptions callOptions);

        Task<State> GetState(int callNumber);
    }
}