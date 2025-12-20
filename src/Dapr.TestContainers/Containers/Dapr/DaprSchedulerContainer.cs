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
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Dapr.TestContainers.Containers.Dapr;

/// <summary>
/// The container for the Dapr scheduler service.
/// </summary>
public sealed class DaprSchedulerContainer : IAsyncStartable
{
	private const int InternalPort = 51005;
	private readonly IContainer _container;
	
    /// <summary>
    /// The container's hostname.
    /// </summary>
	public string Host => _container.Hostname;
    /// <summary>
    /// The container's external port.
    /// </summary>
	public int Port { get; private set; }

    /// <summary>
    /// Creates a new instance of a <see cref="DaprSchedulerContainer"/>.
    /// </summary>
	public DaprSchedulerContainer(DaprRuntimeOptions options, INetwork network)
	{
		// Scheduler service runs via port 51005
		_container = new ContainerBuilder()
			.WithImage(options.SchedulerImageTag)
			.WithName($"scheduler-{Guid.NewGuid():N}")
            .WithNetwork(network)
			.WithCommand("./scheduler", InternalPort.ToString(), "-etcd-data-dir", ".")
			.WithPortBinding(InternalPort, assignRandomHostPort: true)
			.WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(InternalPort))
			.Build();

	}
    
    /// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		await _container.StartAsync(cancellationToken);
		Port = _container.GetMappedPublicPort(InternalPort);
		
		// Empty dirs with 0777 inside container:
		await _container.ExecAsync(["sh", "-c", "mkdir -p ./default-dapr-scheduler-server-0/dapr-0.1 && chmod 0777 ./default-dapr-scheduler-server-0/dapr-0.1"
		], cancellationToken);
		await _container.ExecAsync(["sh", "-c", "mkdir -p ./dapr-scheduler-existing-cluster && chmod 0777 ./dapr-scheduler-existing-cluster"
		], cancellationToken);
	}

    /// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken = default) => _container.StopAsync(cancellationToken);
    /// <inheritdoc />
	public ValueTask DisposeAsync() => _container.DisposeAsync();
}
