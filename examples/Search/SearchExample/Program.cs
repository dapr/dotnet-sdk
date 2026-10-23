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
using Microsoft.Extensions.DependencyInjection;
using SearchExample;

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));

// Search writes can be queued. Wait for completion so the following reads see the changes,
// and fail the request if that cannot be confirmed within the timeout.
var writeOptions = new IndexingOptions
{
    Mode = IndexingMode.WaitForCompletion,
    WaitTimeout = TimeSpan.FromSeconds(20),
    OnWaitTimeout = IndexingWaitTimeoutAction.FailRequest
};

// Both registrations use DAPR_GRPC_ENDPOINT and DAPR_API_TOKEN when supplied.
// A builder-created client belongs to the caller, so dispose it when finished.
using (var client = new DaprSearchClientBuilder().Build())
{
    await RunWithBuilderAsync(client, writeOptions, cancellation.Token);
}

// AddProductSearch is generated from IProductSearch. DI owns the underlying client.
var services = new ServiceCollection();
services.AddDaprSearchClient().AddProductSearch();
await using var provider = services.BuildServiceProvider();
var products = provider.GetRequiredService<IProductSearch>();
await RunWithGeneratedIndexAsync(products, writeOptions, cancellation.Token);

Console.WriteLine("Both Search approaches completed successfully.");
return;

static async Task RunWithBuilderAsync(
    DaprSearchClient client, IndexingOptions writeOptions, CancellationToken cancellationToken)
{
    Console.WriteLine("=== Builder: explicit component, index, and document type ===");
    const string storeName = "searchstore";
    var indexName = $"builder-search-example-{Guid.NewGuid():N}";
    await client.CreateIndexAsync(storeName, indexName, cancellationToken: cancellationToken);

    try
    {
        var documentsToIndex = CreateSampleDocuments();
        var indexingResult = await client.IndexDocumentsAsync(
            storeName, indexName, documentsToIndex, writeOptions, cancellationToken: cancellationToken);
        CheckIndexing(indexingResult);

        var indexInfo = await client.GetIndexAsync(storeName, indexName, cancellationToken: cancellationToken);
        Console.WriteLine($"Index {indexInfo.Index}: {indexInfo.DocumentCount} documents");

        // The document ID is an exact lookup; content is omitted unless explicitly requested.
        var documents = await client.GetDocumentsAsync<Product>(
            storeName, indexName, ["1"], includeContent: true, cancellationToken: cancellationToken);
        CheckDocument(documents);

        // Unlike the ID lookup, a text query finds matches within indexed product content.
        var query = new SearchQuery { Text = "Dapr", IncludeContent = true };
        var searchResult = await client.SearchAsync<Product>(
            storeName, indexName, query, cancellationToken: cancellationToken);
        PrintMatches(searchResult);

        // Listing indexes is a store-wide operation, available only on the general client.
        var indexes = await client.ListIndexesAsync(storeName, cancellationToken: cancellationToken);
        Console.WriteLine($"Store indexes: {string.Join(", ", indexes)}");

        var deletionAck = await client.DeleteDocumentsAsync(
            storeName, indexName, ["1", "2"], writeOptions, cancellationToken: cancellationToken);
        CheckDeletion(deletionAck);

        var remainingDocuments = await client.GetDocumentsAsync<Product>(
            storeName, indexName, ["1", "2"], cancellationToken: cancellationToken);
        CheckNoDocumentsRemain(remainingDocuments);
    }
    finally
    {
        // Give cleanup its own deadline, even if an operation was canceled.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.DeleteIndexAsync(storeName, indexName, cancellationToken: cleanup.Token);
        Console.WriteLine($"Deleted index {indexName}");
    }
}

static async Task RunWithGeneratedIndexAsync(
    IProductSearch products, IndexingOptions writeOptions, CancellationToken cancellationToken)
{
    Console.WriteLine("=== Source generator: component, index, and Product bound by IProductSearch ===");

    // Same lifecycle as above, but IProductSearch supplies the component, index, and Product type.
    await products.CreateIndexAsync(cancellationToken: cancellationToken);

    try
    {
        var documentsToIndex = CreateSampleDocuments();
        var indexingResult = await products.IndexDocumentsAsync(
            documentsToIndex, writeOptions, cancellationToken: cancellationToken);
        CheckIndexing(indexingResult);

        var indexInfo = await products.GetIndexAsync(cancellationToken: cancellationToken);
        Console.WriteLine($"Index {indexInfo.Index}: {indexInfo.DocumentCount} documents");

        var documents = await products.GetDocumentsAsync(
            ["1"], includeContent: true, cancellationToken: cancellationToken);
        CheckDocument(documents);

        var query = new SearchQuery { Text = "Dapr", IncludeContent = true };
        var searchResult = await products.SearchAsync(query, cancellationToken: cancellationToken);
        PrintMatches(searchResult);

        var deletionAck = await products.DeleteDocumentsAsync(
            ["1", "2"], writeOptions, cancellationToken: cancellationToken);
        CheckDeletion(deletionAck);

        var remainingDocuments = await products.GetDocumentsAsync(["1", "2"], cancellationToken: cancellationToken);
        CheckNoDocumentsRemain(remainingDocuments);
    }
    finally
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await products.DeleteIndexAsync(cancellationToken: cleanup.Token);
        Console.WriteLine("Deleted generated-search-example-products");
    }
}

// IDs identify documents independently of their JSON content; only product 1 matches "Dapr".
static IReadOnlyList<SearchDocument<Product>> CreateSampleDocuments() =>
[
    new("1", new Product("Dapr cookbook", 29)),
    new("2", new Product(".NET services", 39))
];

static void CheckIndexing(IndexDocumentsResult result)
{
    // A completed batch can still contain individual document failures.
    if (result.FailedItems.Count != 0)
        throw new InvalidOperationException("Indexing failed: " +
            string.Join("; ", result.FailedItems.Select(item => $"{item.Id}: {item.ErrorMessage}")));
    if (result.Ack != IndexAck.Completed)
        throw new InvalidOperationException($"Expected completed indexing, received {result.Ack}.");
    Console.WriteLine($"Indexed products: {result.Ack}");
}

static void CheckDocument(IReadOnlyList<SearchDocument<Product>> documents)
{
    if (documents.Count != 1 || documents[0].Id != "1" || documents[0].Content != new Product("Dapr cookbook", 29))
        throw new InvalidOperationException("The retrieved product does not match the indexed document.");
    Console.WriteLine($"Retrieved: {documents[0].Content!.Title}");
}

static void PrintMatches(SearchResult<Product> result)
{
    if (result.Hits.Count != 1 || result.Hits[0].Document.Id != "1" ||
        result.Hits[0].Document.Content != new Product("Dapr cookbook", 29))
        throw new InvalidOperationException("The search did not return the expected product.");
    foreach (var hit in result.Hits)
        Console.WriteLine($"Match: {hit.Document.Content!.Title}, price {hit.Document.Content.Price}, score {hit.Score}");
}

static void CheckDeletion(IndexAck ack)
{
    if (ack != IndexAck.Completed)
        throw new InvalidOperationException($"Expected completed deletion, received {ack}.");
    Console.WriteLine($"Deleted products: {ack}");
}

static void CheckNoDocumentsRemain(IReadOnlyList<SearchDocument<Product>> documents)
{
    if (documents.Count != 0)
        throw new InvalidOperationException("Deleted documents are still present.");
}
