// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Test
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITestActor : IActor
    {
        Task<int> GetCountAsync(CancellationToken cancellationToken);

        Task SetCountAsync(int count, CancellationToken cancellationToken);
    }

    public class TestActor : ITestActor
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
