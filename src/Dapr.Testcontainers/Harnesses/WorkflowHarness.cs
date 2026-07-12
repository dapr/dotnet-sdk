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
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;
using Dapr.Testcontainers.Telemetry;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's Workflow building block.
/// </summary>
public sealed class WorkflowHarness : BaseHarness
{
    private readonly RedisContainer _redis;
    private readonly bool _isSelfHostedRedis;
    private DaprTelemetryCollector? _telemetryCollector;
    
    /// <summary>
    /// Provides an implementation harness for Dapr's Workflow building block.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="environment">The isolated environment instance.</param>
    public WorkflowHarness(string componentsDir, Func<int, Task>? startApp,  DaprRuntimeOptions options, DaprTestEnvironment? environment = null) : base(componentsDir, startApp, options, environment)
    {
        _redis = environment?.RedisContainer ?? new RedisContainer(Network, ContainerLogsDirectory);
        _isSelfHostedRedis = environment?.RedisContainer is null;
    }

    /// <summary>
    /// Gets the telemetry collector when telemetry capture has been enabled for this harness.
    /// </summary>
    public DaprTelemetryCollector? TelemetryCollector => _telemetryCollector;

    /// <summary>
    /// Enables Dapr runtime trace export capture for this workflow harness.
    /// When <paramref name="captureWorkflowActivitySource"/> is true, completed
    /// <c>Dapr.Workflow</c> in-process activities are captured from the test application.
    /// </summary>
    /// <param name="captureWorkflowActivitySource">Whether to capture .NET activities emitted by the workflow SDK.</param>
    /// <returns>This workflow harness.</returns>
    public WorkflowHarness EnableTelemetryCapture(bool captureWorkflowActivitySource = true)
    {
        _telemetryCollector ??= new DaprTelemetryCollector();
        if (captureWorkflowActivitySource)
        {
            _telemetryCollector.CaptureActivitySource("Dapr.Workflow");
        }

        return this;
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

        if (_telemetryCollector is not null)
        {
            await _telemetryCollector.StartAsync(cancellationToken);
            WriteWorkflowTracingConfigYaml(ComponentsDirectory, _telemetryCollector.ZipkinEndpointAddressForDapr);
            DaprConfigFilePath = "/components/workflow-tracing-config.yaml";
        }
        
        // Set the service ports
        this.DaprPlacementExternalPort = Environment.PlacementExternalPort;
        this.DaprPlacementAlias = Environment.PlacementAlias;
        this.DaprSchedulerExternalPort = Environment.SchedulerExternalPort;
        this.DaprSchedulerAlias = Environment.SchedulerAlias;
    }

    private static void WriteWorkflowTracingConfigYaml(string componentsDirectory, string zipkinEndpointAddress)
    {
        var yaml = $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: workflowTracing
            spec:
              tracing:
                samplingRate: "1"
                zipkin:
                  endpointAddress: "{{zipkinEndpointAddress}}"
            """;
        Directory.CreateDirectory(componentsDirectory);
        File.WriteAllText(Path.Combine(componentsDirectory, "workflow-tracing-config.yaml"), yaml);
    }
    
    /// <inheritdoc />
	protected override async ValueTask OnDisposeAsync()
    {
        if (_telemetryCollector is not null)
        {
            await _telemetryCollector.DisposeAsync();
        }

        if (_isSelfHostedRedis)
        {
            await _redis.DisposeAsync();
        }
    }
}
