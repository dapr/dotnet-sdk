// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Dapr.Testcontainers.Containers;

/// <summary>A Meilisearch container for the Dapr Search component.</summary>
public sealed class MeilisearchContainer : IAsyncStartable
{
    /// <summary>The pinned default Meilisearch image.</summary>
    public const string DefaultImage = "getmeili/meilisearch:v1.15.2";

    /// <summary>The internal HTTP port.</summary>
    public const int ContainerPort = 7700;

    private readonly IContainer container;
    private readonly ContainerLogAttachment? logAttachment;

    /// <summary>Creates an isolated Meilisearch instance with a randomly generated master key.</summary>
    public MeilisearchContainer(INetwork network, string? logDirectory = null, string image = DefaultImage)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentException.ThrowIfNullOrWhiteSpace(image);
        NetworkAlias = $"meilisearch-{Guid.NewGuid():N}";
        ApiKey = Guid.NewGuid().ToString("N");
        logAttachment = ContainerLogAttachment.TryCreate(logDirectory, "meilisearch", NetworkAlias);
        var builder = new ContainerBuilder()
            .WithImage(image)
            .WithName(NetworkAlias)
            .WithNetwork(network)
            .WithNetworkAliases(NetworkAlias)
            .WithEnvironment("MEILI_MASTER_KEY", ApiKey)
            .WithEnvironment("MEILI_NO_ANALYTICS", "true")
            .WithPortBinding(ContainerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(ContainerPort).ForPath("/health")));
        if (logAttachment is not null)
            builder = builder.WithOutputConsumer(logAttachment.OutputConsumer);
        container = builder.Build();
    }

    /// <summary>The network alias used by the sidecar.</summary>
    public string NetworkAlias { get; }

    /// <summary>The master key for this isolated test instance.</summary>
    public string ApiKey { get; }

    /// <summary>The hostname accessible to the test process.</summary>
    public string Host => container.Hostname;

    /// <summary>The mapped host port, available after startup.</summary>
    public int Port { get; private set; }

    /// <summary>Captured log locations, when enabled.</summary>
    public ContainerLogPaths? LogPaths => logAttachment?.Paths;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await container.StartAsync(cancellationToken);
        Port = container.GetMappedPublicPort(ContainerPort);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => container.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await container.DisposeAsync();
        }
        finally
        {
            if (logAttachment is not null)
                await logAttachment.DisposeAsync();
        }
    }

    /// <summary>Component resource helpers.</summary>
    public static class Yaml
    {
        /// <summary>Writes a search.meilisearch component using the container-network URL, not the mapped host port.</summary>
        public static void WriteSearchStoreYamlToFolder(string folderPath, string host,
            string? apiKey = null, string fileName = "meilisearch-search.yaml")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(host);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            var yaml = $"""
                        apiVersion: dapr.io/v1alpha1
                        kind: Component
                        metadata:
                          name: {Constants.DaprComponentNames.SearchComponentName}
                          namespace: default
                        spec:
                          type: search.meilisearch
                          version: v1
                          metadata:
                          - name: host
                            value: {JsonSerializer.Serialize(host)}
                        """;
            if (apiKey is not null)
                yaml += $"\n  - name: apiKey\n    value: {JsonSerializer.Serialize(apiKey)}";
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(Path.Combine(folderPath, fileName), yaml + "\n");
        }
    }
}
