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

using Dapr.Search;
using Dapr.Search.Extensions;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Search;

public sealed class SearchFixture : IAsyncLifetime
{
    private DaprTestApplication? app;
    private SearchHarness? harness;

    public DaprSearchClient Client => app!.GetRequiredService<DaprSearchClient>();
    public IProductSearch ProductSearch => app!.GetRequiredService<IProductSearch>();

    public async ValueTask InitializeAsync()
    {
        var directory = TestDirectoryManager.CreateTestDirectory("search-components");
        var options = new DaprRuntimeOptions("edge").WithDaprApiToken(Guid.NewGuid().ToString("N"));
        harness = new DaprHarnessBuilder(directory)
            .WithOptions(options)
            .BuildSearch();
        try
        {
            app = await DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(builder => builder.Services.AddDaprSearchClient((provider, clientBuilder) =>
                {
                    var configuration = provider.GetRequiredService<IConfiguration>();
                    clientBuilder.UseGrpcEndpoint(configuration["DAPR_GRPC_ENDPOINT"]!)
                        .UseDaprApiToken(options.DaprApiToken!);
                }).AddProductSearch())
                .BuildAndStartAsync();
        }
        catch
        {
            await harness.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
            await app.DisposeAsync();
    }
}

[SearchIndex("searchstore", "generated-products")]
public partial interface IProductSearch : IDaprSearchIndex<SearchProduct>;

public sealed record SearchProduct(string Title, decimal Price);
