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

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Provides an implementation harness for conversation functionality.
/// </summary>
public sealed class ConversationHarness : BaseHarness
{
    private readonly OllamaContainer _ollama;
    private readonly string componentsDir;

    /// <summary>
    /// Provides an implementation harness for conversation functionality.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The Dapr runtime options.</param>
    public ConversationHarness(string componentsDir, Func<int, Task>? startApp, DaprRuntimeOptions options) : base(componentsDir, startApp, options)
    {
        this.componentsDir = componentsDir;
        _ollama = new(Network);
    }

    /// <inheritdoc />
	protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		// Start infrastructure
		await _ollama.StartAsync(cancellationToken);
		
		// Emit component YAMLs for Ollama (use the default tiny model)
        OllamaContainer.Yaml.WriteConversationYamlToFolder(componentsDir,
            endpoint: $"http://{_ollama.NetworkAlias}:{OllamaContainer.ContainerPort}/v1");
    }

    /// <inheritdoc />
	protected override async ValueTask OnDisposeAsync()
	{
		await _ollama.DisposeAsync();
	}
}
