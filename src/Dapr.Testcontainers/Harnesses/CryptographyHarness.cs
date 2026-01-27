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
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's cryptography building block.
/// </summary>
public sealed class CryptographyHarness : BaseHarness
{
    private readonly string componentsDir;
    private readonly string keyPath;

    /// <summary>
    /// Provides an implementation harness for Dapr's cryptography building block.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="keyPath">The path locally to the cryptography keys to use.</param>
    /// <param name="environment">The isolated environment instance.</param>
    public CryptographyHarness(string componentsDir, Func<int, Task>? startApp, string keyPath, DaprRuntimeOptions options, DaprTestEnvironment? environment = null) : base(componentsDir, startApp, options, environment)
    {
        this.componentsDir = componentsDir;
        this.keyPath = keyPath;
    }

    /// <inheritdoc />
    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		// Emit the component YAML describing the local crypto key store
		LocalStorageCryptographyContainer.Yaml.WriteCryptoYamlToFolder(componentsDir, keyPath);

        return Task.CompletedTask;
    }
}
