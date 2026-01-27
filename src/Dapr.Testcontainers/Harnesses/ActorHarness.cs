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
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;
using Dapr.Testcontainers.Containers.Dapr;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's actor building block.
/// </summary>
public sealed class ActorHarness : BaseHarness
{
    private readonly RedisContainer _redis;
	private readonly DaprPlacementContainer _placement;
	private readonly DaprSchedulerContainer _schedueler;
    private readonly string componentsDir;

    /// <summary>
    /// Provides an implementation harness for Dapr's actor building block.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The dapr runtime options.</param>
    /// <param name="environment">The isolated environment instance.</param>
    public ActorHarness(string componentsDir, Func<int, Task>? startApp, DaprRuntimeOptions options, DaprTestEnvironment? environment = null) : base(componentsDir, startApp, options, environment)
    {
        this.componentsDir = componentsDir;
        _placement = new DaprPlacementContainer(options, Network);
        _schedueler = new DaprSchedulerContainer(options, Network);
        _redis = new(Network);
    }

    /// <inheritdoc />
	protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		// Start infrastructure
		await _redis.StartAsync(cancellationToken);
        await _placement.StartAsync(cancellationToken);
        await _schedueler.StartAsync(cancellationToken);
        
		// Emit component YAMLs pointing to Redis
		RedisContainer.Yaml.WriteStateStoreYamlToFolder(componentsDir, redisHost: $"{_redis.NetworkAlias}:{RedisContainer.ContainerPort}");

        DaprPlacementExternalPort = _placement.ExternalPort;
        DaprSchedulerExternalPort = _schedueler.ExternalPort;
    }
	
    /// <inheritdoc />
	protected override async ValueTask OnDisposeAsync()
	{
		await _redis.DisposeAsync();
		await _placement.DisposeAsync();
		await _schedueler.DisposeAsync();
	}
}
