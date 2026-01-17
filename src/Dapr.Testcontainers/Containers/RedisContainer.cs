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
using Dapr.Testcontainers.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Dapr.Testcontainers.Containers;

/// <summary>
/// Provides a Redis container.
/// </summary>
public sealed class RedisContainer : IAsyncStartable
{
	private const int InternalPort = 6379;
    private readonly string _containerName = $"redis-{Guid.NewGuid():N}";
    
	private readonly IContainer _container;

    /// <summary>
    /// Provides a Redis container.
    /// </summary>
    public RedisContainer(INetwork network)
    {
        _container = new ContainerBuilder()
            .WithImage("redis:alpine")
            .WithName(_containerName)
            .WithNetwork(network)
            .WithPortBinding(InternalPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(InternalPort))
            .Build();
    }

    /// <summary>
    /// The internal container port used by Redis.
    /// </summary>
    public const int ContainerPort = InternalPort;
    /// <summary>
    /// The internal network alias/name of the container.
    /// </summary>
    public string NetworkAlias => _containerName;
    /// <summary>
    /// The hostname of the container.
    /// </summary>
	public string Host => _container.Hostname;
    /// <summary>
    /// The port of the container.
    /// </summary>
	public int Port { get; private set; }

    /// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		await _container.StartAsync(cancellationToken);
		Port = _container.GetMappedPublicPort(InternalPort);
	}
    /// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken = default) => _container.StopAsync(cancellationToken);
    /// <inheritdoc />
    public ValueTask DisposeAsync() => _container.DisposeAsync();

	/// <summary>
	/// Builds out each of the YAML components for Redis
	/// </summary>
	public static class Yaml
	{
        /// <summary>
        /// Writes a state store YAML component.
        /// </summary>
		public static string WriteStateStoreYamlToFolder(string folderPath, string fileName = "redis-state.yaml",string redisHost = "localhost:6379",
			string? passwordSecretName = null)
		{
			var yaml = GetRedisStateStoreYaml(redisHost, passwordSecretName);
			return WriteToFolder(folderPath, fileName, yaml);
		}

        /// <summary>
        /// Writes a distributed lock YAML component.
        /// </summary>
		public static string WriteDistributedLockYamlToFolder(string folderPath, string fileName = "redis-lock.yaml",
			string redisHost = "localhost:6379", string? passwordSecretName = null)
		{
			var yaml = GetDistributedLockYaml(redisHost, passwordSecretName);
			return WriteToFolder(folderPath, fileName, yaml);
		}

		private static string WriteToFolder(string folderPath, string fileName, string yaml)
		{
			Directory.CreateDirectory(folderPath);
			var fullPath = Path.Combine(folderPath, fileName);
			File.WriteAllText(fullPath, yaml);
			return fullPath;
		}

		private static string BuildSecretBlock(string? passwordSecretName) =>
			passwordSecretName is null
				? string.Empty
				: $"  - name: redisPassword\n    secretKeyRef:\n      name: {passwordSecretName}\n      key: redis-password\n";
		
		private static string GetDistributedLockYaml(string redisHost, string? passwordSecretName)
		{
			var secretBlock = BuildSecretBlock(passwordSecretName);
			return
				$@"apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: {Constants.DaprComponentNames.DistributedLockComponentName}
  namespace: default
spec:
  type: lock.redis
  version: v1
  metadata:
  - name: redisHost
    value: {redisHost}
{secretBlock}";
		}
		
		private static string GetRedisStateStoreYaml(string redisHost, string? passwordSecretName)
		{
			var secretBlock = BuildSecretBlock(passwordSecretName);
			return
				$@"apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: {Constants.DaprComponentNames.StateManagementComponentName}
  namespace: default
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: {redisHost}
  - name: actorStateStore
    value: ""true""
{secretBlock}";
		}
	}
}
