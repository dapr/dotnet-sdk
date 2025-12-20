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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapr.TestContainers.Common;
using Dapr.TestContainers.Containers.Dapr;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

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
    ///  A shared Docker network that's safer for CI environments.
    /// </summary>
    protected static readonly INetwork Network = new NetworkBuilder().Build();

    /// <summary>
    /// The HTTP port used by the Daprd container.
    /// </summary>
    public int DaprHttpPort => _daprd?.HttpPort ?? 0;
    /// <summary>
    /// The gRPC port used by the Daprd container.
    /// </summary>
    public int DaprGrpcPort => _daprd?.GrpcPort ?? 0;

    /// <summary>
    /// The specific container startup logic for the harness.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    protected abstract Task OnInitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Initializes and runs the test app with the harness.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Automatically link the Dapr .NET SDK to these containers
        ConfigureSdkEnvironment();
        // Run the actual container orchestration defined in the subclass
        await OnInitializeAsync(cancellationToken);
    }

    /// <summary>
    /// Disposes the resources in this harness.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (_daprd is not null)
            await _daprd.DisposeAsync();
        await Network.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Deletes the specified directory recursively as part of a clean-up operation. 
    /// </summary>
    /// <param name="path">The clean to clean up.</param>
    protected virtual void CleanupComponents(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
    
    private void ConfigureSdkEnvironment()
    {
        if (DaprHttpPort > 0)
            Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", DaprHttpPort.ToString());
        if (DaprGrpcPort > 0)
            Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", DaprGrpcPort.ToString());
    }

}
