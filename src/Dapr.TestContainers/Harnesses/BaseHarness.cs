// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.TestContainers.Common;
using Dapr.TestContainers.Containers.Dapr;

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Provides a base harness for building Dapr building block harnesses.
/// </summary>
public abstract class BaseHarness : IAsyncContainerFixture
{
    /// <summary>
    /// The Daprd container exposed by the harness.
    /// </summary>
    private protected DaprdContainer? _daprd;

    /// <summary>
    /// The HTTP port used by the Daprd container.
    /// </summary>
    public int DaprHttpPort => _daprd?.HttpPort ?? 0;
    /// <summary>
    /// The gRPC port used by the Daprd container.
    /// </summary>
    public int DaprGrpcPort => _daprd?.GrpcPort ?? 0;

    /// <summary>
    /// Initializes and runs the test app with the harness.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public abstract Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disposes the resources in this harness.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (_daprd is not null)
            await _daprd.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
