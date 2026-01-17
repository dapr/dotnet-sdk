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
using Dapr.Testcontainers.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Dapr.Testcontainers.Containers;

/// <summary>
/// Provides an Ollama container.
/// </summary>
public sealed class OllamaContainer : IAsyncStartable
{
	private const int InternalPort = 11434;
    private string _containerName = $"ollama-{Guid.NewGuid():N}";

	private readonly IContainer _container;

    /// <summary>
    /// Provides an Ollama container.
    /// </summary>
    public OllamaContainer(INetwork network)
    {
        _container = new ContainerBuilder()
            .WithImage("ollama/ollama")
            .WithName(_containerName)
            .WithNetwork(network)
            .WithPortBinding(InternalPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(InternalPort))
            .Build();
    }

    /// <summary>
    /// The internal container port used by Ollama.
    /// </summary>
    public const int ContainerPort = InternalPort;
    /// <summary>
    /// The internal network alias/name of the container.
    /// </summary>
    public string NetworkAlias => _containerName;
    /// <summary>
    /// The hostname of the container.
    /// </summary>
	public string Host => _container.Hostname;
    /// <summary>
    /// The port of the container.
    /// </summary>
	public int Port { get; private set; }

    /// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		await _container.StartAsync(cancellationToken);
		Port = _container.GetMappedPublicPort(InternalPort);
	}

    /// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken = default) => _container.StopAsync(cancellationToken);
    /// <inheritdoc />
	public ValueTask DisposeAsync() => _container.DisposeAsync();

	/// <summary>
	/// Builds out the YAML components for Ollama.
	/// </summary>
	public static class Yaml
	{
        /// <summary>
        /// Writes the component YAML.
        /// </summary>
		public static string WriteConversationYamlToFolder(string folderPath, string fileName = "ollama-conversation.yaml", string model = "smollm:135m", string cacheTtl = "10m", string endpoint = "http://localhost:11434/v1")
		{
			var yaml = GetConversationYaml(model, cacheTtl, endpoint);
			return WriteToFolder(folderPath, fileName, yaml);
		}
		
		private static string WriteToFolder(string folderPath, string fileName, string yaml)
		{
			Directory.CreateDirectory(folderPath);
			var fullPath = Path.Combine(folderPath, fileName);
			File.WriteAllText(fullPath, yaml);
			return fullPath;
		}

		private static string GetConversationYaml(string model, string cacheTtl, string endpoint) =>
			$@"apiVersion: dapr.io/v1alpha
kind: Component
metadata:
  name: {Constants.DaprComponentNames.ConversationComponentName}
spec:
  type: conversation.ollama
  metadata:
  - name: model
    value: {model}
  - name: cacheTTL
    value: {cacheTtl}
  - name: endpoint
    value: {endpoint}";
	}
}
