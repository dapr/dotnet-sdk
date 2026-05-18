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
using System.Net;
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
    private const int InternalHealthPort = 8080;
	private readonly IContainer _container;
    private readonly ContainerLogAttachment? _logAttachment;
    private readonly string _containerName = $"dapr-{Guid.NewGuid():N}";

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
    /// <param name="logDirectory">The directory to write container logs to.</param>
    /// <param name="configFilePath">The path inside the container of an optional Dapr configuration YAML file.</param>
    public DaprdContainer(
        string appId, 
        string componentsHostFolder, 
        DaprRuntimeOptions options, 
        INetwork network, 
        HostPortPair? placementHostAndPort = null, 
        HostPortPair? schedulerHostAndPort = null,
        int? daprHttpPort = null,
        int? daprGrpcPort = null,
        string? logDirectory = null,
        string? configFilePath = null
        )
    {
        _requestedHttpPort = daprHttpPort;
        _requestedGrpcPort = daprGrpcPort;
        _logAttachment = ContainerLogAttachment.TryCreate(logDirectory, "daprd", _containerName);
        
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

        if (configFilePath is not null)
        {
            cmd.Add("-config");
            cmd.Add(configFilePath);
        }

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

        var containerBuilder = new ContainerBuilder()
            .WithImage(options.RuntimeImageTag)
            .WithName(_containerName)
            .WithLogger(ConsoleLogger.Instance)
            .WithCommand(cmd.ToArray())
            .WithNetwork(network)
            .WithExtraHost(ContainerHostAlias, "host-gateway")
            .WithBindMount(componentsHostFolder, componentsPath, AccessMode.ReadOnly)
            // Wait for the canonical "fully initialized" log line that daprd emits only after
            // every server (HTTP, internal gRPC, and *API* gRPC on port 50001) is running.
            // Previous versions waited on "Internal gRPC server is running", which is the
            // sidecar-to-sidecar internal gRPC server and is logged *before* the public API
            // gRPC server is up. That produced a small race in which the harness reported
            // readiness while the API gRPC port still refused connections, surfacing as
            // intermittent "Status(StatusCode=Unavailable, Detail=Error connecting to
            // subchannel., ... Connection refused)" errors on the first client call.
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Dapr sidecar is up and running."));

        if (_logAttachment is not null)
        {
            containerBuilder = containerBuilder.WithOutputConsumer(_logAttachment.OutputConsumer);
        }

        // Put the API token in an envvar so it can be picked up by the Dapr runtime at startup
        if (!string.IsNullOrWhiteSpace(options.DaprApiToken))
        {
            containerBuilder = containerBuilder.WithEnvironment("DAPR_API_TOKEN", options.DaprApiToken);
        }

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
        await ContainerReadinessProbe.WaitForTcpPortAsync("127.0.0.1", HttpPort, TimeSpan.FromSeconds(30), cancellationToken);
        await ContainerReadinessProbe.WaitForTcpPortAsync("127.0.0.1", GrpcPort, TimeSpan.FromSeconds(30), cancellationToken);

        // The container log wait above ("Dapr sidecar is up and running.") guarantees that
        // daprd's HTTP server, internal gRPC server, *and* API gRPC server are all running
        // inside the container. As an additional safety net, poll the HTTP port on the host
        // until any HTTP response is observed. This confirms that Docker's port forwarding for
        // the HTTP port has finished wiring up. The matching guarantee for the gRPC API port is
        // provided by the WaitForTcpPortAsync probe above plus the canonical container wait
        // message — which together rule out the "Error connecting to subchannel / Connection
        // refused" race that previously occurred when the harness completed startup before the
        // API gRPC server was actually listening.
        await ContainerReadinessProbe.WaitForHttpReachableAsync(
            $"http://127.0.0.1:{HttpPort}/v1.0/healthz",
            TimeSpan.FromSeconds(30),
            cancellationToken);
    }

    /// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken = default) => _container.StopAsync(cancellationToken);
    /// <inheritdoc />
	public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();

        if (_logAttachment is not null)
        {
            await _logAttachment.DisposeAsync();
        }
    }

    /// <summary>
    /// Gets the log file locations for this container.
    /// </summary>
    public ContainerLogPaths? LogPaths => _logAttachment?.Paths;
}
