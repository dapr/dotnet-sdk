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
using Dapr.TestContainers.Containers;
using Dapr.TestContainers.Containers.Dapr;

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's Workflow building block.
/// </summary>
/// <param name="componentsDir">The directory to Dapr components.</param>
/// <param name="startApp">The test app to validate in the harness.</param>
/// <param name="options">The Dapr runtime options.</param>
public sealed class WorkflowHarness(string componentsDir, Func<Task<int>> startApp,  DaprRuntimeOptions options) : BaseHarness
{
	private readonly RedisContainer _redis = new(Network);
	private readonly DaprPlacementContainer _placement = new(options, Network);
	private readonly DaprSchedulerContainer _scheduler = new(options, Network);

    /// <inheritdoc />
	protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		// 1) Start Redis (actor state store)
		await _redis.StartAsync(cancellationToken);
		
		// 2) Emit component YAMLs pointing to Redis
		RedisContainer.Yaml.WriteStateStoreYamlToFolder(componentsDir, redisHost: $"{_redis.Host}:{_redis.Port}");
		
		// 3) Start placement
		await _placement.StartAsync(cancellationToken);
		
		// 4) Start scheduler
		await _scheduler.StartAsync(cancellationToken);
		
		// 5) Start the test
		var actualAppPort = await startApp();
		
		// 6) Configure and start daprd; point at placement & scheduler
		_daprd = new DaprdContainer(
			appId: options.AppId,
			componentsHostFolder: componentsDir,
		 	options: options with {AppPort = actualAppPort},
            Network,
			new HostPortPair(_placement.Host, _placement.Port),
			new HostPortPair(_scheduler.Host, _scheduler.Port));
		await _daprd.StartAsync(cancellationToken);
	}

    /// <inheritdoc />
	public override async ValueTask DisposeAsync()
	{
		if (_daprd is not null) 
			await _daprd.DisposeAsync();
		await _placement.DisposeAsync();
		await _scheduler.DisposeAsync();
		await _redis.DisposeAsync();
        
        // Cleanup the generated YAML files
        CleanupComponents(componentsDir);
	}
}
