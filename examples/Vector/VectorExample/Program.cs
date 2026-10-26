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
using System.Text.Json.Serialization;
using Dapr.Vector;
using Dapr.Vector.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDaprVectorClient((_, builder) =>
    builder.UseJsonSerializationOptions(ProductJsonContext.Default.Options)).AddProductVectors();
using var provider = services.BuildServiceProvider();
var vectors = provider.GetRequiredService<IProductVectors>();
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var ct = timeout.Token;
var wait = new IndexingOptions
{
    Mode = IndexingMode.WaitForCompletion,
    WaitTimeout = TimeSpan.FromSeconds(20),
    OnWaitTimeout = IndexingWaitTimeoutAction.FailRequest
};

await vectors.CreateCollectionAsync(3, DistanceMetric.Cosine, cancellationToken: ct);
try
{
    var attributes = JsonSerializer.Deserialize<JsonElement>("""{"category":"books"}""");
    var write = await vectors.UpsertVectorsAsync(
        [
            new("dapr", [1, 0, 0], new Product("Dapr guide"), attributes),
            new("distributed", [0.8f, 0.6f, 0], new Product("Distributed systems"), attributes)
        ], wait,
        cancellationToken: ct);
    Console.WriteLine($"Upsert acknowledgement: {write.Ack}");
    foreach (var failure in write.FailedItems)
        Console.Error.WriteLine($"{failure.Id}: {failure.ErrorCode} {failure.ErrorMessage}");
    if (write.FailedItems.Count > 0)
        throw new InvalidOperationException("One or more vectors failed to index.");

    var query = new VectorQuery
    {
        Vector = [1, 0, 0],
        TopK = 2,
        IncludePayload = true,
        IncludeValues = true,
        Filter = JsonSerializer.Deserialize<JsonElement>("""{"category":{"$eq":"books"}}""")
    };
    var result = await vectors.QueryVectorsAsync(query, cancellationToken: ct);
    foreach (var match in result.Matches)
        Console.WriteLine($"{match.Record.Id}: {match.Record.Payload?.Title}, {result.Metric} score {match.Score}");

    var batch = await vectors.BatchQueryVectorsAsync(
        [query, new() { ById = "dapr", IncludePayload = true }, new() { ById = "missing" }], cancellationToken: ct);
    for (var i = 0; i < batch.Count; i++)
    {
        if (batch[i].Error is { } error)
            Console.WriteLine($"Query {i}: {error.Code} {error.Message}");
        else
            Console.WriteLine($"Query {i}: {batch[i].Response!.Matches.Count} matches");
    }

    Console.WriteLine(
        $"Delete acknowledgement: {await vectors.DeleteVectorsAsync(["dapr", "distributed"], wait, cancellationToken: ct)}");
}
finally
{
    await vectors.DeleteCollectionAsync(cancellationToken: ct);
}

using var directClient = new DaprVectorClientBuilder().Build();
Console.WriteLine(
    $"Remaining collections: {string.Join(", ", await directClient.ListCollectionsAsync("vectorstore", cancellationToken: ct))}");

[VectorCollection("vectorstore", "sdk-vector-example")]
public partial interface IProductVectors : IDaprVectorCollection<Product>;

public sealed record Product(string Title);

[JsonSerializable(typeof(Product))]
internal partial class ProductJsonContext : JsonSerializerContext;
