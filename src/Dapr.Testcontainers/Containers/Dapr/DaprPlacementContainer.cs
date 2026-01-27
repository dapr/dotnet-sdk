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
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Dapr.Testcontainers.Containers.Dapr;

/// <summary>
/// The container for the Dapr placement service.
/// </summary>
public sealed class DaprPlacementContainer : IAsyncStartable
{
	private readonly IContainer _container;
    private readonly string _containerName = $"placement-{Guid.NewGuid():N}";

    /// <summary>
    /// The internal network alias/name of the container.
    /// </summary>
    public string NetworkAlias => _containerName;
    /// <summary>
    /// The container hostname.
    /// </summary>
	public string Host => _container.Hostname;
    /// <summary>
    /// The container's external port.
    /// </summary>
	public int ExternalPort { get; private set; }
    /// <summary>
    /// THe contains' internal port.
    /// </summary>
    public const int InternalPort = 50006;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprPlacementContainer"/>.
    /// </summary>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="network">The shared Docker network to connect to.</param>
    public DaprPlacementContainer(DaprRuntimeOptions options, INetwork network)
	{
		//Placement service runs via port 50006
		_container = new ContainerBuilder()
			.WithImage(options.PlacementImageTag)
			.WithName(_containerName)
            .WithNetwork(network)
			.WithCommand("./placement", "-port", InternalPort.ToString())
			.WithPortBinding(InternalPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("placement server leadership acquired"))
			.Build();
	}

    /// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		await _container.StartAsync(cancellationToken);
		ExternalPort = _container.GetMappedPublicPort(InternalPort);
	}

    /// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken = default) => _container.StopAsync(cancellationToken);
    /// <inheritdoc />
	public ValueTask DisposeAsync() => _container.DisposeAsync();
}
