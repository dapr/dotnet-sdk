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

using Dapr.Metadata.Abstractions;
using Dapr.Metadata.Extensions;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.IntegrationTests.Metadata;

public sealed class MetadataIntegrationTests
{
    [Fact]
    public async Task AddDaprMetadata_LoadsMetadataFromRunningSidecar()
    {
        var appId = $"metadata-app-{Guid.NewGuid():N}";
        await using var testApp = await StartMetadataTestAppAsync(new DaprRuntimeOptions().WithAppId(appId));

        using var scope = testApp.CreateScope();
        var metadata = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<DaprMetadata>>().CurrentValue;

        Assert.Equal(appId, metadata.AppId);
        Assert.False(string.IsNullOrWhiteSpace(metadata.RuntimeVersion));
        Assert.NotNull(metadata.EnabledFeatures);
        Assert.NotNull(metadata.Components);
        Assert.NotNull(metadata.CustomAttributes);
        Assert.NotNull(metadata.AppConnectionProperties);
        Assert.NotNull(metadata.SchedulerMetadata);
        Assert.NotNull(metadata.Workflows);
    }

    [Fact]
    public async Task AddDaprMetadata_UsesConfiguredDaprApiToken()
    {
        var appId = $"metadata-token-app-{Guid.NewGuid():N}";
        var token = $"token-{Guid.NewGuid():N}";
        var options = new DaprRuntimeOptions()
            .WithAppId(appId)
            .WithDaprApiToken(token);

        await using var testApp = await StartMetadataTestAppAsync(options, token);

        using var scope = testApp.CreateScope();
        var metadata = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<DaprMetadata>>().CurrentValue;

        Assert.Equal(appId, metadata.AppId);
        Assert.False(string.IsNullOrWhiteSpace(metadata.RuntimeVersion));
    }

    [Fact]
    public async Task AddDaprMetadata_ProvidesConsistentMetadataThroughOptionsAbstractions()
    {
        var appId = $"metadata-options-app-{Guid.NewGuid():N}";
        await using var testApp = await StartMetadataTestAppAsync(new DaprRuntimeOptions().WithAppId(appId));

        using var scope = testApp.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DaprMetadata>>();
        var monitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<DaprMetadata>>();

        Assert.Equal(appId, options.Value.AppId);
        Assert.Equal(options.Value.AppId, monitor.CurrentValue.AppId);
        Assert.Equal(options.Value.RuntimeVersion, monitor.CurrentValue.RuntimeVersion);
    }

    private static async Task<DaprTestApplication> StartMetadataTestAppAsync(
        DaprRuntimeOptions options,
        string? daprApiToken = null)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("metadata-components");

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(options)
            .BuildMetadata();

        try
        {
            return await DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(builder =>
                {
                    if (!string.IsNullOrWhiteSpace(daprApiToken))
                    {
                        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["DAPR_API_TOKEN"] = daprApiToken
                        });
                    }

                    builder.Services.AddDaprMetadata();
                })
                .BuildAndStartAsync();
        }
        catch
        {
            await harness.DisposeAsync();
            throw;
        }
    }
}
