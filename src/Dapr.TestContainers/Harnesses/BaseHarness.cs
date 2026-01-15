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
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Containers.Dapr;
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
    /// Indicates whether the sidecar ports are ready for use.
    /// </summary>
    private readonly TaskCompletionSource _sidecarPortsReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>
    /// The isolated network and Dapr environment this harness is using.
    /// </summary>
    private readonly DaprTestEnvironment _environment;
    /// <summary>
    /// Tracks whether the environment was created implicitly (standalone mode) or is shared (controlling whether we dispose of it).
    /// </summary>
    private readonly bool _ownsEnvironment;
    
    private readonly string componentsDirectory;
    private readonly Func<int, Task>? startApp;
    private readonly DaprRuntimeOptions options;

    /// <summary>
    /// Provides a base harness for building Dapr building block harnesses.
    /// </summary>
    protected BaseHarness(string componentsDirectory, Func<int, Task>? startApp, DaprRuntimeOptions options, DaprTestEnvironment? environment = null)
    {
        this.componentsDirectory = componentsDirectory;
        this.startApp = startApp;
        this.options = options;

        if (environment is not null)
        {
            // Shared mode - we use an existing environment
            _environment = environment;
            _ownsEnvironment = false;
        }
        else
        {
            // Standalone mode - create a dedicated environment for this harness
            _environment = new DaprTestEnvironment(options);
            _ownsEnvironment = true;
        }
    }

    /// <summary>
    /// Gets the shared Docker network used by this instance.
    /// </summary>
    public INetwork Network => _environment.Network;

    /// <summary>
    /// Gets the shared environment instance.
    /// </summary>
    protected DaprTestEnvironment Environment => _environment;

    /// <summary>
    /// Gets the port that the Dapr sidecar is configured to talk to - this is the port the test application should use.
    /// </summary>
    public int AppPort { get; private set; }

    /// <summary>
    /// The HTTP port used by the Daprd container.
    /// </summary>
    public int DaprHttpPort => _daprd?.HttpPort ?? 0;

    /// <summary>
    /// The HTTP endpoint used by the Daprd container.
    /// </summary>
    public string DaprHttpEndpoint => $"http://{DaprdContainer.ContainerHostAlias}:{DaprHttpPort}";
    
    /// <summary>
    /// The gRPC port used by the Daprd container.
    /// </summary>
    public int DaprGrpcPort => _daprd?.GrpcPort ?? 0;

    /// <summary>
    /// The gRPC endpoint used by the Daprd container.
    /// </summary>
    public string DaprGrpcEndpoint => $"http://{DaprdContainer.ContainerHostAlias}:{DaprGrpcPort}";

    /// <summary>
    /// The Dapr components directory.
    /// </summary>
    protected string ComponentsDirectory => componentsDirectory;
    
    /// <summary>
    /// The port of the Dapr placement service, if started.
    /// </summary>
    protected int? DaprPlacementExternalPort { get; set; }
    
    /// <summary>
    /// The network alias of the placement container, if started.
    /// </summary>
    protected string? DaprPlacementAlias { get; set; }
    
    /// <summary>
    /// The port of the Dapr scheduler service, if started.
    /// </summary>
    protected int? DaprSchedulerExternalPort { get; set; }
    
    /// <summary>
    /// The network alias of the scheduler container, if started.
    /// </summary>
    protected string? DaprSchedulerAlias { get; set; }
    
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
        // Ensure the environment infrastructure is running
        // If we own it, we start it. If it's shared, the caller usually starts it
        // but calling StartAsync is idempotent, so it's safe to ensure it here
        await _environment.StartAsync(cancellationToken);
        
        // Run the actual container orchestration defined in the subclass to set up any pre-requisite containers
        // before loading daprd and the start app, if specified
        await OnInitializeAsync(cancellationToken);
        
        // PIck a port only when we are ready to start, or use 0 to let the OS decide
        this.AppPort = PortUtilities.GetAvailablePort();
        
        // Configure and start daprd; point at placement & scheduler
        _daprd = new DaprdContainer(
            appId: options.AppId,
            componentsHostFolder: ComponentsDirectory,
            options: options with {AppPort = this.AppPort},
            Network,
            DaprPlacementExternalPort is null || DaprPlacementAlias is null 
                ? null : new HostPortPair(DaprPlacementAlias, DaprPlacementContainer.InternalPort),
            DaprSchedulerExternalPort is null || DaprSchedulerAlias is null 
                ? null : new HostPortPair(DaprSchedulerAlias, DaprSchedulerContainer.InternalPort));

        var daprdTask = Task.Run(async () =>
        {
            await _daprd!.StartAsync(cancellationToken);
            _sidecarPortsReady.TrySetResult();
        }, cancellationToken);

        Task? appTask = null;
        if (startApp is not null)
        {
            appTask = Task.Run(async () =>
            {
                await _sidecarPortsReady.Task.WaitAsync(cancellationToken);
                
                await startApp(AppPort);
            }, cancellationToken);
        }

        await Task.WhenAll(daprdTask, appTask ?? Task.CompletedTask);
    }

    /// <summary>
    /// Disposes the resources in this harness.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        await OnDisposeAsync();
        
        if (_daprd is not null)
            await _daprd.DisposeAsync();
        
        // Only dispose of the environment if this harness created it (Standalone mode)
        // If it was passed in (Shared mode), the test class is responsible for disposing it
        if (_ownsEnvironment)
        {
            await _environment.DisposeAsync();
        }
        
        // Clean up generated YAML files
        TestDirectoryManager.CleanUpDirectory(ComponentsDirectory);
        
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override this to dispose harness-specific resources before base cleanup.
    /// </summary>
    protected virtual ValueTask OnDisposeAsync() => ValueTask.CompletedTask;
}
