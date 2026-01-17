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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;
using Dapr.Testcontainers.Containers.Dapr;
using Dapr.Testcontainers.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Represents a shared testing environment for Dapr applications, providing common infrastructure
/// like a shared Docker network, Placement service, Scheduler service, and Redis state store.
/// Use this environment to connect multiple application harnesses to the same Dapr control plane.
/// </summary>
public sealed class DaprTestEnvironment : IAsyncDisposable
{
    private readonly DaprPlacementContainer _placement;
    private readonly DaprSchedulerContainer _scheduler;
    private readonly RedisContainer? _redis;
    private bool _started;
    private readonly bool _ownsNetwork;
    private readonly DockerNetworkLease? _networkLease;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTestEnvironment"/> class.
    /// </summary>
    /// <param name="options">Optional Dapr runtime options. If null, default options are used.</param>
    /// <param name="needsActorState">True if a </param>
    /// <param name="network">Optional shared Docker network. If null, a dedicated network is created.</param>
    public DaprTestEnvironment(DaprRuntimeOptions? options = null, bool needsActorState = false, INetwork? network = null)
    {
        options ??= new DaprRuntimeOptions();

        if (network is null)
        {
            Network = new NetworkBuilder().Build();
            _ownsNetwork = true;
        }
        else
        {
            Network = network;
            _ownsNetwork = false;
        }
        
        _placement = new DaprPlacementContainer(options, Network);
        _scheduler = new DaprSchedulerContainer(options, Network);
        
        if (needsActorState)
        {
            _redis = new RedisContainer(Network);
        }
    }

    private DaprTestEnvironment(
        DockerNetworkLease networkLease,
        DaprRuntimeOptions? options = null,
        bool needsActorState = false) : this(options, needsActorState, networkLease.Network)
    {
        _networkLease = networkLease;
    }

    /// <summary>
    /// Gets the shared Docker network used by containers in this environment.
    /// </summary>
    public INetwork Network { get; }

    /// <summary>
    /// Exposes the redis container, if loaded in the environment.
    /// </summary>
    public RedisContainer? RedisContainer => _redis;

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
    /// Creates a <see cref="DaprTestEnvironment"/> with a pooled network for testing purposes.
    /// </summary>
    public static async ValueTask<DaprTestEnvironment> CreateWithPooledNetworkAsync(
        DaprRuntimeOptions? options = null,
        bool needsActorState = false,
        CancellationToken cancellationToken = default)
    {
        var lease = await DockerNetworkPool.RentAsync(cancellationToken).ConfigureAwait(false);
        return new DaprTestEnvironment(lease, options, needsActorState);
    }

    /// <summary>
    /// Starts the environment infrastructure (Network, Redis, Placement, Scheduler).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started) return;

        List<Task> infrastructureTasks =
        [
            _placement.StartAsync(cancellationToken),
            _scheduler.StartAsync(cancellationToken)
        ];

        if (_redis is not null)
            infrastructureTasks.Add(_redis.StartAsync(cancellationToken));
        
        // Start infrastructure containers in parallel
        await Task.WhenAll(infrastructureTasks);

        _started = true;
    }
    
    /// <summary>
    /// Stops and disposes of the environment resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _placement.DisposeAsync();
        await _scheduler.DisposeAsync();

        if (_redis is not null)
            await _redis.DisposeAsync();
        
        if (_ownsNetwork)
            await Network.DisposeAsync();

        if (_networkLease is not null)
            await _networkLease.DisposeAsync();
    }
}
