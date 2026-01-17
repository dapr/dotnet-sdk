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

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's Workflow building block.
/// </summary>
public sealed class WorkflowHarness : BaseHarness
{
    private readonly RedisContainer _redis;
    private readonly bool _isSelfHostedRedis;
    
    /// <summary>
    /// Provides an implementation harness for Dapr's Workflow building block.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="environment">The isolated environment instance.</param>
    public WorkflowHarness(string componentsDir, Func<int, Task>? startApp,  DaprRuntimeOptions options, DaprTestEnvironment? environment = null) : base(componentsDir, startApp, options, environment)
    {
        _redis = environment?.RedisContainer ?? new RedisContainer(Network);
        _isSelfHostedRedis = environment?.RedisContainer is null;
    }

    /// <inheritdoc />
	protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
	{
        // If we're self-hosting Redis and it's not provided through the test environment, start it
        if (_isSelfHostedRedis)
        {
            await _redis.StartAsync(cancellationToken);
        }
        
        // Emit component YAMLs pointing to Redis
        RedisContainer.Yaml.WriteStateStoreYamlToFolder(ComponentsDirectory, redisHost: $"{_redis.NetworkAlias}:{RedisContainer.ContainerPort}");
        
        // Set the service ports
        this.DaprPlacementExternalPort = Environment.PlacementExternalPort;
        this.DaprPlacementAlias = Environment.PlacementAlias;
        this.DaprSchedulerExternalPort = Environment.SchedulerExternalPort;
        this.DaprSchedulerAlias = Environment.SchedulerAlias;
    }
    
    /// <inheritdoc />
	protected override ValueTask OnDisposeAsync() => ValueTask.CompletedTask;
}
