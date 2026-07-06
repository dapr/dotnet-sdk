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
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
    private readonly string _containerName = $"ollama-{Guid.NewGuid():N}";

	private readonly IContainer _container;
    private readonly ContainerLogAttachment? _logAttachment;

    /// <summary>
    /// Provides an Ollama container.
    /// </summary>
    public OllamaContainer(INetwork network, string? logDirectory = null)
    {
        _logAttachment = ContainerLogAttachment.TryCreate(logDirectory, "ollama", _containerName);

        var containerBuilder = new ContainerBuilder()
            .WithImage("ollama/ollama")
            .WithName(_containerName)
            .WithNetwork(network)
            .WithEnvironment("CUDA_VISIBLE_DEVICES", "-1")
            .WithPortBinding(InternalPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(InternalPort))
            ;

        if (_logAttachment is not null)
        {
            containerBuilder = containerBuilder.WithOutputConsumer(_logAttachment.OutputConsumer);
        }

        _container = containerBuilder.Build();
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

    /// <summary>
    /// Ensures the indicated model is available in the running Ollama instance.
    /// </summary>
    /// <param name="model">The model name to pull if missing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureModelAsync(string model, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        
        if (Port <= 0)
            throw new InvalidOperationException("Ollama container must be started before pulling models.");

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri($"http://127.0.0.1:{Port}");

        if (await IsModelAvailableAsync(httpClient, model, cancellationToken))
            return;

        var payload = JsonSerializer.Serialize(new { name = model });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync("/api/pull", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await DrainResponseAsync(response.Content, cancellationToken);
    }

    /// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		await _container.StartAsync(cancellationToken);
		Port = _container.GetMappedPublicPort(InternalPort);
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

    private static async Task<bool> IsModelAvailableAsync(
        HttpClient httpClient,
        string model,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync("/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("models", out var models))
                return false;

            foreach (var entry in models.EnumerateArray())
            {
                if (!entry.TryGetProperty("name", out var nameElement))
                    continue;

                if (string.Equals(nameElement.GetString(), model, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    private static async Task DrainResponseAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[8192];

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
                break;
        }
    }

	/// <summary>
	/// Builds out the YAML components for Ollama.
	/// </summary>
	public static class Yaml
	{
        /// <summary>
        /// Writes the component YAML.
        /// </summary>
		public static void WriteConversationYamlToFolder(string folderPath, string model, string fileName = "ollama-conversation.yaml", string cacheTtl = "10m", string endpoint = "http://localhost:11434/v1")
		{
			var yaml = GetConversationYaml(model, cacheTtl, endpoint);
			WriteToFolder(folderPath, fileName, yaml);
        }
		
		private static void WriteToFolder(string folderPath, string fileName, string yaml)
		{
			Directory.CreateDirectory(folderPath);
			var fullPath = Path.Combine(folderPath, fileName);
			File.WriteAllText(fullPath, yaml);
        }

		private static string GetConversationYaml(string model, string cacheTtl, string endpoint) =>
            $"""
             apiVersion: dapr.io/v1alpha1
             kind: Component
             metadata:
               name: {Constants.DaprComponentNames.ConversationComponentName}
             spec:
               type: conversation.ollama
               version: v1
               metadata:
               - name: model
                 value: {model}
               - name: cacheTTL
                 value: {cacheTtl}
               - name: endpoint
                 value: {endpoint}
             """;
	}
}
