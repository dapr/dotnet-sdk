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
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Containers.Dapr;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Represents a shared testing environment for Dapr applications, providing common infrastructure
/// like a shared Docker network, Placement service, Scheduler service, and Redis state store.
/// Use this environment to connect multiple application harnesses to the same Dapr control plane.
/// </summary>
public sealed class DaprTestEnvironment : IAsyncDisposable
{
    private readonly DaprPlacementContainer _placement;
    private readonly DaprSchedulerContainer _scheduler;
    private bool _started;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTestEnvironment"/> class.
    /// </summary>
    /// <param name="options">Optional Dapr runtime options. If null, default options are used.</param>
    public DaprTestEnvironment(DaprRuntimeOptions? options = null)
    {
        options ??= new DaprRuntimeOptions();
        Network = new NetworkBuilder().Build();

        _placement = new DaprPlacementContainer(options, Network);
        _scheduler = new DaprSchedulerContainer(options, Network);
    }

    /// <summary>
    /// Gets the shared Docker network used by containers in this environment.
    /// </summary>
    public INetwork Network { get; }

    /// <summary>
    /// Provides the external port used by the Dapr Placement service.
    /// </summary>
    public int PlacementExternalPort => _placement.ExternalPort;
    
    /// <summary>
    /// Gets the alias of the Placement container on the shared network.
    /// </summary>
    public string PlacementAlias => _placement.NetworkAlias;

    /// <summary>
    /// Provides the external port used by the Dapr Scheduler service.
    /// </summary>
    public int SchedulerExternalPort => _scheduler.ExternalPort;

    /// <summary>
    /// Gets the alias of the Scheduler container on the shared network.
    /// </summary>
    public string SchedulerAlias => _scheduler.NetworkAlias;

    /// <summary>
    /// Starts the environment infrastructure (Network, Redis, Placement, Scheduler).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started) return;
        
        // Start infrastructure containers in parallel
        await Task.WhenAll(
            _placement.StartAsync(cancellationToken),
            _scheduler.StartAsync(cancellationToken))
            ;

        _started = true;
    }
    
    /// <summary>
    /// Stops and disposes of the environment resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _placement.DisposeAsync();
        await _scheduler.DisposeAsync();
        await Network.DisposeAsync();
    }
}
