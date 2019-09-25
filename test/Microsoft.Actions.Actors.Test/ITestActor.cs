// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Test
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for test actor.
    /// </summary>
    internal interface ITestActor : IActor
    {
        /// <summary>
        /// GetCount method for TestActor.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The current count as stored in actor.</returns>
        Task<int> GetCountAsync(CancellationToken cancellationToken);

        /// <summary>
        /// SetCoutn method for test actor.
        /// </summary>
        /// <param name="count">Count to set for the actor.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>Task.</returns>
        Task SetCountAsync(int count, CancellationToken cancellationToken);
    }

    internal class TestActor : ITestActor
    {
        public Task<int> GetCountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(5);
        }

        public Task SetCountAsync(int count, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
