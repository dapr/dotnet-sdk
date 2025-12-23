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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Containers.Dapr;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Provides a base harness for building Dapr building block harnesses.
/// </summary>
public abstract class BaseHarness(string componentsDirectory, Func<int, Task>? startApp, DaprRuntimeOptions options) : IAsyncContainerFixture
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
    /// Gets the port that the Dapr sidecar is configured to talk to - this is the port the test application should use.
    /// </summary>
    public int AppPort { get; private protected set; } = PortUtilities.GetAvailablePort();

    /// <summary>
    /// The HTTP port used by the Daprd container.
    /// </summary>
    public int DaprHttpPort => _daprd?.HttpPort ?? 0;
    
    /// <summary>
    /// The gRPC port used by the Daprd container.
    /// </summary>
    public int DaprGrpcPort => _daprd?.GrpcPort ?? 0;

    /// <summary>
    /// The Dapr components directory.
    /// </summary>
    protected string ComponentsDirectory => componentsDirectory;
    
    /// <summary>
    /// The port of the Dapr placement service, if started.
    /// </summary>
    protected int? DaprPlacementPort { get; set; }
    
    /// <summary>
    /// The port of the Dapr scheduler service, if started.
    /// </summary>
    protected int? DaprSchedulerPort { get; set; }
    
    /// <summary>
    /// The specific container startup logic for the harness.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected abstract Task OnInitializeAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Initializes and runs the test app with the harness.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Run the actual container orchestration defined in the subclass to set up any pre-requisite containers before loading daprd and the start app, if specified
        await OnInitializeAsync(cancellationToken);
        
        // Configure and start daprd; point at placement & scheduler
        _daprd = new DaprdContainer(
            appId: options.AppId,
            componentsHostFolder: ComponentsDirectory,
            options: options with {AppPort = this.AppPort},
            Network,
            DaprPlacementPort is null ? null : new HostPortPair("host.docker.internal", DaprPlacementPort.Value),
            DaprSchedulerPort is null ? null : new HostPortPair("host.docker.internal", DaprSchedulerPort.Value));
        
        // Create a list of tasks to run concurrently - namely, start daprd and the startApp, if specified
        var tasks = new List<Task>
        {
            _daprd.StartAsync(cancellationToken)
        };

        if (startApp is not null)
        {
            tasks.Add(startApp(this.AppPort));
        }

        // Wait for both to start
        await Task.WhenAll(tasks);
        
        // Now that _daprd is populated and ports are assigned, automatically link the Dapr .NET SDK to these
        // containers via environment variables.
        // This ensures that when the test body creates a DaprClient, it finds the right ports.
        if (DaprHttpPort > 0)
            Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", DaprHttpPort.ToString());
        if (DaprGrpcPort > 0)
            Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", DaprGrpcPort.ToString());
    }

    /// <summary>
    /// Disposes the resources in this harness.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (_daprd is not null)
            await _daprd.DisposeAsync();
        await Network.DisposeAsync();
        
        // Clean up generated YAML files
        CleanupComponents(ComponentsDirectory);
        
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Deletes the specified directory recursively as part of a clean-up operation. 
    /// </summary>
    /// <param name="path">The clean to clean up.</param>
    protected static void CleanupComponents(string path)
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
}
