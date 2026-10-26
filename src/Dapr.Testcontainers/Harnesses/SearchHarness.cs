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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>Runs the Dapr Search building block with an isolated Meilisearch container.</summary>
public sealed class SearchHarness : BaseHarness
{
    private readonly MeilisearchContainer meilisearch;

    /// <summary>Creates a harness using a Dapr runtime with Search support.</summary>
    public SearchHarness(string componentsDir, Func<int, Task>? startApp, DaprRuntimeOptions options,
        DaprTestEnvironment? environment = null, string meilisearchImage = MeilisearchContainer.DefaultImage)
        : base(componentsDir, startApp, options, environment)
    {
        meilisearch = new MeilisearchContainer(Network, ContainerLogsDirectory, meilisearchImage);
    }

    /// <inheritdoc />
    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        await meilisearch.StartAsync(cancellationToken);
        MeilisearchContainer.Yaml.WriteSearchStoreYamlToFolder(ComponentsDirectory,
            $"http://{meilisearch.NetworkAlias}:{MeilisearchContainer.ContainerPort}", meilisearch.ApiKey);
    }

    /// <inheritdoc />
    protected override ValueTask OnDisposeAsync() => meilisearch.DisposeAsync();
}
