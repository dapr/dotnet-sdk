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
using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Dapr.TestContainers.Containers.Dapr;

/// <summary>
/// The container for the Dapr runtime.
/// </summary>
public sealed class DaprdContainer : IAsyncStartable
{
	private const int InternalHttpPort = 3500;
	private const int InternalGrpcPort = 50001;
	private readonly IContainer _container;
	
    /// <summary>
    /// The HTTP port of the Dapr runtime.
    /// </summary>
	public int HttpPort { get; private set; }
    /// <summary>
    /// The gRPC port of the Dapr runtime.
    /// </summary>
	public int GrpcPort { get; private set; }

    /// <summary>
    /// Used to initialize a new instance of a <see cref="DaprdContainer"/>..
    /// </summary>
    /// <param name="appId">The ID of the app to initialize daprd with.</param>
    /// <param name="componentsHostFolder">The path to the Dapr resources directory.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="netowrk">The shared Docker network to connect to.</param>
    /// <param name="placementHostAndPort">The hostname and port of the Placement service.</param>
    /// <param name="schedulerHostAndPort">The hostname and port of the Scheduler service.</param>
    public DaprdContainer(string appId, string componentsHostFolder, DaprRuntimeOptions options, INetwork netowrk, HostPortPair? placementHostAndPort = null, HostPortPair? schedulerHostAndPort = null)
    {
        const string componentsPath = "/components";
		var cmd =
			new List<string>
			{
                "/daprd",
				"-app-id", appId,
				"-app-port", options.AppPort.ToString(),
                "-app-channel-address", "host.docker.internal",
				"-dapr-http-port", InternalHttpPort.ToString(),
				"-dapr-grpc-port", InternalGrpcPort.ToString(),
				"-log-level", options.LogLevel.ToString().ToLowerInvariant(),
				"-resources-path", componentsPath
			};

		if (placementHostAndPort is not null)
		{
			cmd.Add("-placement-host-address");
			cmd.Add(placementHostAndPort.ToString());
		}
        else
        {
            // Explicitly disable placement if not provided to speed up startup
            cmd.Add("-placement-host-address");
            cmd.Add("");
        }

		if (schedulerHostAndPort is not null)
		{
			cmd.Add("-scheduler-host-address");
			cmd.Add(schedulerHostAndPort.ToString());
		}
		
		_container = new ContainerBuilder()
			.WithImage(options.RuntimeImageTag)
			.WithName($"dapr-{Guid.NewGuid():N}")
            .WithLogger(ConsoleLogger.Instance)
			.WithCommand(cmd.ToArray())
            .WithNetwork(netowrk)
            .WithExtraHost("host.docker.internal", "host-gateway")
			.WithPortBinding(InternalHttpPort, assignRandomHostPort: true)
			.WithPortBinding(InternalGrpcPort, assignRandomHostPort: true)
			.WithBindMount(componentsHostFolder, componentsPath, AccessMode.ReadOnly)
			.WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Dapr sidecar is up and running."))
			.Build();
	}

    /// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		await _container.StartAsync(cancellationToken);
		HttpPort = _container.GetMappedPublicPort(InternalHttpPort);
		GrpcPort = _container.GetMappedPublicPort(InternalGrpcPort);

        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", HttpPort.ToString());
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", GrpcPort.ToString());
    }

    /// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken = default) => _container.StopAsync(cancellationToken);
    /// <inheritdoc />
	public ValueTask DisposeAsync() => _container.DisposeAsync();
}
