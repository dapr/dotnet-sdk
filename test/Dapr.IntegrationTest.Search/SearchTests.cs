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

using System.Text.Json;
using Dapr.Common;
using Dapr.Search;
using Dapr.Testcontainers;
using Grpc.Core;

namespace Dapr.IntegrationTest.Search;

public sealed class SearchTests(SearchFixture fixture) : IClassFixture<SearchFixture>
{
    private const string Store = Constants.DaprComponentNames.SearchComponentName;
    private DaprSearchClient Client => fixture.Client;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly IndexingOptions Wait = new()
    {
        Mode = IndexingMode.WaitForCompletion,
        WaitTimeout = TimeSpan.FromSeconds(20),
        OnWaitTimeout = IndexingWaitTimeoutAction.FailRequest
    };

    [SearchFact]
    public async Task GeneratedIndexCompletesTypedDocumentLifecycle()
    {
        var index = fixture.ProductSearch;
        await index.CreateIndexAsync(cancellationToken: Ct);
        try
        {
            var product = new SearchProduct("Dapr source generation", 25);
            var write = await index.IndexDocumentsAsync([new SearchDocument<SearchProduct>("1", product)], Wait,
                cancellationToken: Ct);
            Assert.Equal(IndexAck.Completed, write.Ack);
            Assert.Empty(write.FailedItems);
            var info = await index.GetIndexAsync(cancellationToken: Ct);
            Assert.Equal("generated-products", info.Index);
            Assert.Equal(1UL, info.DocumentCount);
            Assert.Equal(product, Assert.Single(await index.GetDocumentsAsync(["1"], true, cancellationToken: Ct)).Content);
            var result = await index.SearchAsync(new SearchQuery { Text = "generation", IncludeContent = true },
                cancellationToken: Ct);
            Assert.Equal(product, Assert.Single(result.Hits).Document.Content);
            Assert.Equal(IndexAck.Completed, await index.DeleteDocumentsAsync(["1"], Wait, cancellationToken: Ct));
            Assert.Empty(await index.GetDocumentsAsync(["1"], true, cancellationToken: Ct));
        }
        finally
        {
            await index.DeleteIndexAsync(cancellationToken: Ct);
        }
        Assert.DoesNotContain("generated-products", await Client.ListIndexesAsync(Store, cancellationToken: Ct));
    }

    [SearchFact]
    public async Task IndexAndDocumentLifecycleRoundTrips()
    {
        var index = NewIndex();
        await Client.CreateIndexAsync(Store, index, cancellationToken: Ct);
        try
        {
            Assert.Contains(index, await Client.ListIndexesAsync(Store, cancellationToken: Ct));
            var info = await Client.GetIndexAsync(Store, index, cancellationToken: Ct);
            Assert.Equal(index, info.Index);
            Assert.Equal(0UL, info.DocumentCount);
            var metadata = new Dictionary<string, string> { ["source"] = "integration-test" };
            var write = await Client.IndexDocumentsAsync(Store, index,
                new[] { new SearchDocument<Product>("1", new("Dapr book", "books", 10), metadata) }, Wait,
                cancellationToken: Ct);
            Assert.Equal(IndexAck.Completed, write.Ack);
            Assert.Empty(write.FailedItems);
            Assert.Equal(1UL, (await Client.GetIndexAsync(Store, index, cancellationToken: Ct)).DocumentCount);

            var found = Assert.Single(await Client.GetDocumentsAsync<Product>(Store, index, ["missing", "1"], true,
                cancellationToken: Ct));
            Assert.Equal("1", found.Id);
            Assert.Equal(new Product("Dapr book", "books", 10), found.Content);
            Assert.Equal("integration-test", found.Metadata!["source"]);
            var noContent =
                Assert.Single(await Client.GetDocumentsAsync<Product>(Store, index, ["1"], cancellationToken: Ct));
            Assert.Null(noContent.Content);
            Assert.Equal("integration-test", noContent.Metadata!["source"]);

            write = await Client.IndexDocumentsAsync(Store, index,
                new[] { new SearchDocument<Product>("1", new("Updated book", "books", 15)) }, Wait,
                cancellationToken: Ct);
            Assert.Empty(write.FailedItems);
            Assert.Equal("Updated book",
                Assert.Single(await Client.GetDocumentsAsync<Product>(Store, index, ["1"], true, cancellationToken: Ct))
                    .Content!.Title);
            Assert.Equal(IndexAck.Completed,
                await Client.DeleteDocumentsAsync(Store, index, ["1"], Wait, cancellationToken: Ct));
            Assert.Empty(await Client.GetDocumentsAsync<Product>(Store, index, ["1"], true, cancellationToken: Ct));
        }
        finally
        {
            await Client.DeleteIndexAsync(Store, index, cancellationToken: Ct);
        }

        Assert.DoesNotContain(index, await Client.ListIndexesAsync(Store, cancellationToken: Ct));
    }

    [SearchFact]
    public async Task SearchesFiltersSortsProjectsHighlightsAndPaginates()
    {
        var index = NewIndex();
        await Client.CreateIndexAsync(Store, index,
            new Dictionary<string, string>
            {
                ["filterableAttributes"] = "category,price",
                ["sortableAttributes"] = "price",
                ["searchableAttributes"] = "title"
            }, Ct);
        try
        {
            var documents = new[]
            {
                new SearchDocument<Product>("a", new("Dapr introduction", "books", 30)),
                new SearchDocument<Product>("b", new("Dapr advanced", "books", 10)),
                new SearchDocument<Product>("c", new("Dapr reference", "books", 20)),
                new SearchDocument<Product>("d", new("Dapr video", "videos", 5))
            };
            var indexed = await Client.IndexDocumentsAsync(Store, index, documents, Wait, cancellationToken: Ct);
            Assert.Equal(IndexAck.Completed, indexed.Ack);
            Assert.Empty(indexed.FailedItems);

            var query = new SearchQuery
            {
                Text = "Dapr",
                IncludeContent = true,
                TopK = 2,
                Filter = JsonSerializer.Deserialize<JsonElement>("""{"category":{"$eq":"books"}}"""),
                Sort = [new SortClause("price")],
                SearchFields = ["title"],
                ReturnFields = ["title", "price"],
                HighlightFields = ["title"]
            };
            var page = await Client.SearchAsync<JsonElement>(Store, index, query, cancellationToken: Ct);
            Assert.Equal(["b", "c"], page.Hits.Select(h => h.Document.Id));
            Assert.NotNull(page.TotalHits);
            Assert.True(page.TotalHits >= 3);
            Assert.NotEqual(TotalHitsRelation.Unspecified, page.TotalHitsRelation);
            Assert.False(string.IsNullOrEmpty(page.ContinuationToken));
            Assert.All(page.Hits, hit =>
            {
                Assert.True(hit.Document.Content.TryGetProperty("title", out _));
                Assert.False(hit.Document.Content.TryGetProperty("category", out _));
                Assert.Contains("<em>", hit.Highlights["title"]);
            });
            var lastPage = await Client.SearchAsync<JsonElement>(Store, index,
                query with { ContinuationToken = page.ContinuationToken }, cancellationToken: Ct);
            Assert.Equal("a", Assert.Single(lastPage.Hits).Document.Id);
            Assert.True(string.IsNullOrEmpty(lastPage.ContinuationToken));
            var matchAll = await Client.SearchAsync<Product>(Store, index, cancellationToken: Ct);
            Assert.Equal(4, matchAll.Hits.Count);
            Assert.All(matchAll.Hits, hit => Assert.Null(hit.Document.Content));
        }
        finally
        {
            await Client.DeleteIndexAsync(Store, index, cancellationToken: Ct);
        }
    }

    [SearchFact]
    public async Task SupportsNativeQueries()
    {
        var index = NewIndex();
        await Client.CreateIndexAsync(Store, index, cancellationToken: Ct);
        try
        {
            var result = await Client.IndexDocumentsAsync(Store, index,
                new[] { new SearchDocument<Product>("native", new("Dapr guide", "books", 10)) }, Wait,
                cancellationToken: Ct);
            Assert.Empty(result.FailedItems);
            var response = await Client.SearchAsync<Product>(Store, index,
                new SearchQuery
                {
                    Native = JsonSerializer.Deserialize<JsonElement>("""{"q":"Dapr"}"""),
                    IncludeContent = true
                }, cancellationToken: Ct);
            Assert.Equal("Dapr guide", Assert.Single(response.Hits).Document.Content!.Title);
        }
        finally
        {
            await Client.DeleteIndexAsync(Store, index, cancellationToken: Ct);
        }
    }

    [SearchFact]
    public async Task ReportsPartialFailuresWithoutDiscardingSuccessfulDocuments()
    {
        var index = NewIndex();
        await Client.CreateIndexAsync(Store, index, cancellationToken: Ct);
        try
        {
            var result = await Client.IndexDocumentsAsync(Store, index,
                new[]
                {
                    new SearchDocument<JsonElement>("good",
                        JsonSerializer.Deserialize<JsonElement>("""{"title":"Dapr"}""")),
                    new SearchDocument<JsonElement>("bad", JsonSerializer.Deserialize<JsonElement>("[]"))
                }, Wait, cancellationToken: Ct);
            Assert.Equal(IndexAck.Completed, result.Ack);
            var failure = Assert.Single(result.FailedItems);
            Assert.Equal("bad", failure.Id);
            Assert.Equal((int)StatusCode.InvalidArgument, failure.ErrorCode);
            Assert.NotEmpty(failure.ErrorMessage);
            Assert.Equal("good",
                Assert.Single(await Client.GetDocumentsAsync<JsonElement>(Store, index, ["good", "bad"], true,
                    cancellationToken: Ct)).Id);
        }
        finally
        {
            await Client.DeleteIndexAsync(Store, index, cancellationToken: Ct);
        }
    }

    [SearchFact]
    public async Task DefaultWritesAcknowledgeAcceptanceAndEventuallyBecomeVisible()
    {
        var index = NewIndex();
        await Client.CreateIndexAsync(Store, index, cancellationToken: Ct);
        try
        {
            var result = await Client.IndexDocumentsAsync(Store, index,
                new[] { new SearchDocument<Product>("queued", new("Dapr", "books", 10)) }, cancellationToken: Ct);
            Assert.Equal(IndexAck.Queued, result.Ack);
            Assert.Empty(result.FailedItems);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            while ((await Client.GetDocumentsAsync<Product>(Store, index, ["queued"], true,
                       cancellationToken: timeout.Token)).Count == 0)
                await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
            Assert.Equal(IndexAck.Queued,
                await Client.DeleteDocumentsAsync(Store, index, ["queued"], cancellationToken: Ct));
            while ((await Client.GetDocumentsAsync<Product>(Store, index, ["queued"], cancellationToken: timeout.Token))
                   .Count != 0)
                await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
        finally
        {
            await Client.DeleteIndexAsync(Store, index, cancellationToken: Ct);
        }
    }

    [SearchFact]
    public async Task PreservesProviderErrors()
    {
        var error = await Assert.ThrowsAsync<DaprException>(() =>
            Client.GetIndexAsync(Store, NewIndex(), cancellationToken: Ct));
        Assert.Equal(StatusCode.NotFound, Assert.IsType<RpcException>(error.InnerException).StatusCode);
    }

    private static string NewIndex() => $"sdk-{Guid.NewGuid():N}";

    public sealed record Product(string Title, string Category, int Price);
}
