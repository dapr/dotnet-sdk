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
using Dapr.TestContainers.Containers;
using Dapr.TestContainers.Containers.Dapr;

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's cryptography building block.
/// </summary>
/// <param name="componentsDir">The directory to Dapr components.</param>
/// <param name="startApp">The test app to validate in the harness.</param>
/// <param name="options">The Dapr runtime options.</param>
/// <param name="keyPath">The path locally to the cryptography keys to use.</param>
public sealed class CryptographyHarness(string componentsDir, Func<int, Task>startApp, string keyPath, DaprRuntimeOptions options) : BaseHarness
{
    /// <inheritdoc />
    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		// Emit the component YAML describing the local crypto key store
		LocalStorageCryptographyContainer.Yaml.WriteCryptoYamlToFolder(componentsDir, keyPath);
		
        // Find a random free port for the test app
        var assignedAppPort = PortUtilities.GetAvailablePort();
        
		// Configure and start daprd
		_daprd = new DaprdContainer(
			appId: options.AppId,
			componentsHostFolder: componentsDir,
			options: options with {AppPort = assignedAppPort},
            Network);
		await _daprd.StartAsync(cancellationToken);
        
        // Start the app
        await startApp(assignedAppPort);
	}
	
    /// <inheritdoc />
	public override async ValueTask DisposeAsync()
	{
		if (_daprd is not null)
			await _daprd.DisposeAsync();
        
        // Cleanup the generated YAML files
        CleanupComponents(componentsDir);
	}
}
