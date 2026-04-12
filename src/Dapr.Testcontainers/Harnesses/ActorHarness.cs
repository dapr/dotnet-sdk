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
/// Provides an implementation harness for Dapr's actor building block.
/// </summary>
public sealed class ActorHarness : BaseHarness
{
    private readonly RedisContainer _redis;
    private readonly bool _isSelfHostedRedis;
    private readonly string _componentsDir;

    /// <summary>
    /// Provides an implementation harness for Dapr's actor building block.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="environment">
    /// An optional shared <see cref="DaprTestEnvironment"/>. When provided the harness reuses
    /// its Redis, Placement, and Scheduler services instead of starting its own.
    /// </param>
    public ActorHarness(string componentsDir, Func<int, Task>? startApp, DaprRuntimeOptions options, DaprTestEnvironment? environment = null)
        : base(componentsDir, startApp, options, environment)
    {
        _componentsDir = componentsDir;
        _redis = environment?.RedisContainer ?? new RedisContainer(Network, ContainerLogsDirectory);
        _isSelfHostedRedis = environment?.RedisContainer is null;
    }

    /// <inheritdoc />
    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        // Only start Redis if it is not provided by a shared environment.
        if (_isSelfHostedRedis)
        {
            await _redis.StartAsync(cancellationToken);
        }

        // Write the state-store component YAML that points to the Redis instance.
        RedisContainer.Yaml.WriteStateStoreYamlToFolder(
            _componentsDir,
            redisHost: $"{_redis.NetworkAlias}:{RedisContainer.ContainerPort}");

        // Forward placement and scheduler coordinates from the environment.
        DaprPlacementExternalPort = Environment.PlacementExternalPort;
        DaprPlacementAlias = Environment.PlacementAlias;
        DaprSchedulerExternalPort = Environment.SchedulerExternalPort;
        DaprSchedulerAlias = Environment.SchedulerAlias;
    }

    /// <inheritdoc />
    protected override async ValueTask OnDisposeAsync()
    {
        if (_isSelfHostedRedis)
        {
            await _redis.DisposeAsync();
        }
    }
}
