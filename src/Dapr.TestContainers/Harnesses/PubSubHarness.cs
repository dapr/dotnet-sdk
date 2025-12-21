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
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Containers;
using Dapr.TestContainers.Containers.Dapr;

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's pub/sub building block.
/// </summary>
/// <param name="componentsDir">The directory to Dapr components.</param>
/// <param name="startApp">The test app to validate in the harness.</param>
/// <param name="options">The Dapr runtime options.</param>
public sealed class PubSubHarness(string componentsDir, Func<Task<int>> startApp, DaprRuntimeOptions options) : BaseHarness
{
	private readonly RabbitMqContainer _rabbitmq = new(Network);
    
    /// <inheritdoc />
	protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		// 1) Start RabbitMq (pubsub)
		await _rabbitmq.StartAsync(cancellationToken);
		
		// 2) Emit component YAMLs pointing to RabbitMQ
		RabbitMqContainer.Yaml.WritePubSubYamlToFolder(componentsDir);
		
		// 3) Start the test app
		var actualAppPort = await startApp();
		
		// 4) Configure & start daprd
		_daprd = new DaprdContainer(
			appId: options.AppId,
			componentsHostFolder: componentsDir,
			options: options with {AppPort = actualAppPort},
            Network);
		await _daprd.StartAsync(cancellationToken);
	}

    /// <inheritdoc />
	public override async ValueTask DisposeAsync()
	{
		if (_daprd is not null)
			await _daprd.DisposeAsync();
		await _rabbitmq.DisposeAsync();
        
        // Cleanup the generated YAML files
        CleanupComponents(componentsDir);
	}
}
