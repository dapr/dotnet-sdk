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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Dapr.Testcontainers.Containers.Dapr;

/// <summary>
/// The container for the Dapr scheduler service.
/// </summary>
public sealed class DaprSchedulerContainer : IAsyncStartable
{
	private readonly IContainer _container;
    private readonly ContainerLogAttachment? _logAttachment;
    // Contains the data directory used by this instance of the Dapr scheduler service
    //private readonly string _hostDataDir = Path.Combine(Path.GetTempPath(), $"dapr-scheduler-{Guid.NewGuid():N}");
    private readonly string _testDirectory;
    private readonly string _containerName = $"scheduler-{Guid.NewGuid():N}";

    /// <summary>
    /// The internal network alias/name of the container.
    /// </summary>
    public string NetworkAlias => _containerName;
    /// <summary>
    /// The container's hostname.
    /// </summary>
	public string Host => _container.Hostname;
    /// <summary>
    /// The container's external port.
    /// </summary>
	public int ExternalPort { get; private set; }
    /// <summary>
    /// The container's internal port.
    /// </summary>
    public const int InternalPort = 51005;
    /// <summary>
    /// The container's internal health port.
    /// </summary>
    private const int HealthPort = 8080;

    /// <summary>
    /// Creates a new instance of a <see cref="DaprSchedulerContainer"/>.
    /// </summary>
	public DaprSchedulerContainer(DaprRuntimeOptions options, INetwork network, string? logDirectory = null)
	{
        _logAttachment = ContainerLogAttachment.TryCreate(logDirectory, "scheduler", _containerName);

		// Scheduler service runs via port 51005
        const string containerDataDir = "/data/dapr-scheduler";
        string[] cmd =
        [
            "./scheduler",
            "--port", InternalPort.ToString(),
            "--etcd-data-dir", containerDataDir
        ];

        _testDirectory = TestDirectoryManager.CreateTestDirectory("scheduler");

        var containerBuilder = new ContainerBuilder()
            .WithImage(options.SchedulerImageTag)
            .WithName(_containerName)
            .WithNetwork(network)
            .WithCommand(cmd.ToArray())
            .WithPortBinding(InternalPort, assignRandomHostPort: true)
            .WithPortBinding(HealthPort, assignRandomHostPort: true) // Allows probes to reach healthz
            // Mount an anonymous volume to /data to ensure the scheduler has write permissions
            .WithBindMount(_testDirectory, containerDataDir, AccessMode.ReadWrite)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(endpoint =>
                        endpoint
                            .ForPort(HealthPort)
                            .ForPath("/healthz")
                            .ForStatusCodeMatching(code => (int)code >= 200 && (int)code < 300),
                    mod =>
                        mod
                            .WithTimeout(TimeSpan.FromMinutes(2))
                            .WithInterval(TimeSpan.FromSeconds(5))
                            .WithMode(WaitStrategyMode.Running)));

        if (_logAttachment is not null)
        {
            containerBuilder = containerBuilder.WithOutputConsumer(_logAttachment.OutputConsumer);
        }

        _container = containerBuilder.Build();
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
	public async ValueTask DisposeAsync()
    {
        // Remove the data directory if it exists
        TestDirectoryManager.CleanUpDirectory(_testDirectory);

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
