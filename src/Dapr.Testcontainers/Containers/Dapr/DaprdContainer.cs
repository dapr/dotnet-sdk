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
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Dapr.Testcontainers.Containers.Dapr;

/// <summary>
/// The container for the Dapr runtime.
/// </summary>
public sealed class DaprdContainer : IAsyncStartable
{
	private const int InternalHttpPort = 3500;
	private const int InternalGrpcPort = 50001;
	private readonly IContainer _container;
    private string _containerName = $"dapr-{Guid.NewGuid():N}";

    /// <summary>
    /// The internal network alias/name of the container.
    /// </summary>
    public string NetworkAlias => _containerName;
    /// <summary>
    /// The HTTP port of the Dapr runtime.
    /// </summary>
	public int HttpPort { get; private set; }
    /// <summary>
    /// The gRPC port of the Dapr runtime.
    /// </summary>
	public int GrpcPort { get; private set; }

    private readonly int? _requestedHttpPort;
    private readonly int? _requestedGrpcPort;

    /// <summary>
    /// The hostname to locate the Dapr runtime on in the shared Docker network.
    /// </summary>
    public const string ContainerHostAlias = "host.docker.internal";

    /// <summary>
    /// Used to initialize a new instance of a <see cref="DaprdContainer"/>..
    /// </summary>
    /// <param name="appId">The ID of the app to initialize daprd with.</param>
    /// <param name="componentsHostFolder">The path to the Dapr resources directory.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="network">The shared Docker network to connect to.</param>
    /// <param name="placementHostAndPort">The hostname and port of the Placement service.</param>
    /// <param name="schedulerHostAndPort">The hostname and port of the Scheduler service.</param>
    /// <param name="daprHttpPort">The host HTTP port to bind to.</param>
    /// <param name="daprGrpcPort">The host gRPC port to bind to.</param>
    public DaprdContainer(
        string appId, 
        string componentsHostFolder, 
        DaprRuntimeOptions options, 
        INetwork network, 
        HostPortPair? placementHostAndPort = null, 
        HostPortPair? schedulerHostAndPort = null,
        int? daprHttpPort = null,
        int? daprGrpcPort = null
        )
    {
        _requestedHttpPort = daprHttpPort;
        _requestedGrpcPort = daprGrpcPort;
        
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
        else
        {
            // Explicitly disable scheduler if not provider
            cmd.Add("-scheduler-host-address");
            cmd.Add("");
        }
		
		var  containerBuilder = new ContainerBuilder()
			.WithImage(options.RuntimeImageTag)
			.WithName(_containerName)
            .WithLogger(ConsoleLogger.Instance)
			.WithCommand(cmd.ToArray())
            .WithNetwork(network)
            .WithExtraHost(ContainerHostAlias, "host-gateway")
			.WithBindMount(componentsHostFolder, componentsPath, AccessMode.ReadOnly)
			.WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Internal gRPC server is running"));
                //.UntilMessageIsLogged(@"^dapr initialized. Status: Running. Init Elapsed "))

            containerBuilder = daprHttpPort is not null ? containerBuilder.WithPortBinding(containerPort: InternalHttpPort, hostPort: daprHttpPort.Value) : containerBuilder.WithPortBinding(port: InternalHttpPort, assignRandomHostPort: true);
            containerBuilder = daprGrpcPort is not null ? containerBuilder.WithPortBinding(containerPort: InternalGrpcPort, hostPort: daprGrpcPort.Value) : containerBuilder.WithPortBinding(port: InternalGrpcPort, assignRandomHostPort: true);
                
            _container = containerBuilder.Build();
	}

    /// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		await _container.StartAsync(cancellationToken);

        var mappedHttpPort = _container.GetMappedPublicPort(InternalHttpPort);
        var mappedGrpcPort = _container.GetMappedPublicPort(InternalGrpcPort);

        if (_requestedHttpPort is not null && mappedHttpPort != _requestedHttpPort.Value)
        {
            throw new InvalidOperationException(
                $"Dapr HTTP port mapping mismatch. Requested {_requestedHttpPort.Value}, but Docker mapped {mappedHttpPort}");
        }

        if (_requestedGrpcPort is not null && mappedGrpcPort != _requestedGrpcPort.Value)
        {
            throw new InvalidOperationException(
                $"Dapr gRPC port mapping mismatch. Requested {_requestedGrpcPort.Value}, but Docker mapped {mappedGrpcPort}");
        }

        HttpPort = mappedHttpPort;
        GrpcPort = mappedGrpcPort;

        // The container log wait strategy can fire before the host port is actually accepting connections
        // (especially on Windows). Ensure the ports are reachable from the test process.
        await WaitForTcpPortAsync("127.0.0.1", HttpPort, TimeSpan.FromSeconds(30), cancellationToken);
        await WaitForTcpPortAsync("127.0.0.1", GrpcPort, TimeSpan.FromSeconds(30), cancellationToken); 
    }

    private static async Task WaitForTcpPortAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);

                var completed = await Task.WhenAny(connectTask,
                    Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken));
                if (completed == connectTask)
                {
                    // Will throw if connect failed
                    await connectTask;
                    return;
                }
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for TCP port {host}:{port} to accept connections.", lastError);
    }

    /// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken = default) => _container.StopAsync(cancellationToken);
    /// <inheritdoc />
	public ValueTask DisposeAsync() => _container.DisposeAsync();
}
